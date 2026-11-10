using AiInterview.Common.Configuration;
using AiInterview.Common.Constants;
using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.Data.Models;
using AiInterview.Data.Repositories.Questions;
using AiInterview.Data.Repositories.Sessions;
using AiInterview.Data.Repositories.Turns;
using AiInterview.Services.Mapping;
using AiInterview.Services.Retrieval;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Services.Interview;

/// <summary>
/// Vòng đời phiên thi: tạo, bắt đầu, trả trạng thái, nhận câu trả lời, bỏ qua câu, kết thúc.
/// Luật cứng nằm ở <see cref="HardRules"/>, chuyển trạng thái ở <see cref="InterviewStateMachine"/>.
/// </summary>
internal sealed class InterviewSessionService(
    ISessionRepository sessions,
    IQuestionRepository questions,
    IQuestionUsageRepository usage,
    ISessionStateRepository states,
    ITurnPublisher publisher,
    IOptions<InterviewOptions> options,
    ILogger<InterviewSessionService> logger,
    IFollowUpDecider? decider = null,
    IRetrievalService? retrieval = null,
    TimeProvider? timeProvider = null) : IInterviewSessionService
{
    private const string File = "InterviewSessionService.cs";

    /// <summary>Số câu hỏi gần nhất dùng để tránh lặp câu giữa các thí sinh thi liền nhau.</summary>
    private const int RecentQuestionsWindow = 200;

    /// <summary>Khóa chống trùng chỉ cần sống lâu hơn một lượt trả lời bình thường.</summary>
    private static readonly TimeSpan TurnLockTtl = TimeSpan.FromMinutes(5);

    private readonly InterviewOptions _defaults = options.Value;

    private DateTime UtcNow => (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<SessionDto> CreateAsync(CreateSessionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.StudentName))
        {
            throw new ArgumentException("Student name is required.", nameof(request));
        }

        var questionCount = request.QuestionCount ?? _defaults.QuestionCount;
        var maxFollowUps = request.MaxFollowUps ?? _defaults.MaxFollowUps;
        var answerSeconds = request.AnswerSeconds ?? _defaults.AnswerSeconds;

        if (questionCount <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Question count must be greater than 0.");
        if (maxFollowUps < 0) throw new ArgumentOutOfRangeException(nameof(request), "Max follow-ups cannot be negative.");
        if (answerSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Answer time must be greater than 0.");

        var session = new Data.Entities.InterviewSession
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? $"Interview for {request.StudentName}" : request.Title.Trim(),
            StudentName = request.StudentName.Trim(),
            Status = SessionStatus.Created,
            QuestionCount = questionCount,
            MaxFollowUps = maxFollowUps,
            AnswerSeconds = answerSeconds,
            CreatedAt = UtcNow
        };

        // Chọn câu hỏi ngay khi tạo để biết ngân hàng có đủ câu hay không (báo lỗi sớm cho giảng viên).
        var questionIds = await PickQuestionsAsync(questionCount, ct);
        await sessions.AddAsync(session, ct);

        // Ghi ngay để thí sinh thi liền sau không nhận lại đúng tập câu này.
        await usage.RecordAsync(session.Id, questionIds, ct);

        await states.SaveAsync(session.Id.ToString(), new SessionState
        {
            SessionId = session.Id.ToString(),
            State = InterviewState.AskQuestion,
            QuestionIndex = 0,
            QuestionId = questionIds[0],
            QuestionIds = questionIds.ToList()
        }, ct);

        logger.Info(File, ServiceNames.App,
            "Tạo phiên thi, sessionId={SessionId}, student={Student}, questions={QuestionCount}, maxFollowUps={MaxFollowUps}, answerSeconds={AnswerSeconds}",
            session.Id, session.StudentName, questionCount, maxFollowUps, answerSeconds);

        return session.ToDto();
    }

    public async Task<InterviewStateView> StartAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, ct);

        if (session.Status == SessionStatus.Completed || session.Status == SessionStatus.Abandoned)
        {
            throw new InvalidOperationException($"This interview is already {session.Status}.");
        }

        var state = await states.GetAsync(sessionId.ToString(), ct);
        var recovered = state is null;

        if (state is null)
        {
            // Trạng thái Redis đã hết TTL hoặc chưa từng lưu: chọn lại câu hỏi thay vì làm sập phiên.
            var questionIds = await PickQuestionsAsync(session.QuestionCount, ct);
            state = new SessionState
            {
                SessionId = sessionId.ToString(),
                State = InterviewState.AskQuestion,
                QuestionIndex = 0,
                QuestionId = questionIds[0],
                QuestionIds = questionIds.ToList()
            };
            await usage.RecordAsync(sessionId, questionIds, ct);
            logger.Warn(File, ServiceNames.App, "Không tìm thấy trạng thái, chọn lại câu hỏi, sessionId={SessionId}", sessionId);
        }

        // Bắt đầu từ câu đang dở (nếu có) hoặc câu đầu tiên.
        var questionId = state.QuestionId ?? state.QuestionIds.ElementAtOrDefault(0);
        var question = questionId == Guid.Empty ? null : await questions.GetAsync(questionId, ct);

        if (question is null)
        {
            throw new InvalidOperationException("The selected question no longer exists. Please create a new interview.");
        }

        // Câu hỏi chính: nội dung luôn lấy bản mới nhất trong ngân hàng. Câu xoáy thì giữ nguyên của LLM.
        if (!state.IsFollowUp)
        {
            state.CurrentQuestionText = question.Content;
        }

        // Đọc lại câu hỏi không được cấp thêm thời gian: chỉ đặt deadline khi chưa có (lần bắt đầu đầu tiên).
        state.Deadline ??= UtcNow.AddSeconds(session.AnswerSeconds);
        state.TurnStartedAt ??= UtcNow;
        state.SilencePrompts = 0;

        if (session.Status == SessionStatus.Created)
        {
            session.Status = SessionStatus.InProgress;
            session.StartedAt = UtcNow;
            await sessions.UpdateAsync(session, ct);
        }

        await states.SaveAsync(sessionId.ToString(), state, ct);

        logger.Info(File, ServiceNames.App,
            "Bắt đầu/khôi phục phiên thi, sessionId={SessionId}, questionId={QuestionId}, isFollowUp={IsFollowUp}, recovered={Recovered}",
            sessionId, question.Id, state.IsFollowUp, recovered);

        return ToView(session, state);
    }

    public async Task<InterviewStateView?> GetStateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await sessions.GetAsync(sessionId, ct);
        if (session is null)
        {
            return null;
        }

        var state = await states.GetAsync(sessionId.ToString(), ct);
        if (state is null)
        {
            return null;
        }

        // Câu hỏi chính có thể đã bị sửa nội dung: luôn ưu tiên bản mới nhất trong ngân hàng,
        // nhưng với câu hỏi xoáy thì chỉ còn bản lưu trong state.
        if (!state.IsFollowUp && state.QuestionId is { } id)
        {
            var question = await questions.GetAsync(id, ct);
            if (question is not null)
            {
                state.CurrentQuestionText = question.Content;
            }
        }

        return ToView(session, state);
    }

    public async Task<SubmitAnswerResult> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TurnId))
        {
            throw new ArgumentException("TurnId is required.", nameof(request));
        }

        var sessionId = request.SessionId.ToString();

        // ENG-06: request trùng (client gửi lại) không được gọi LLM lần hai.
        if (!await states.TryLockAsync(sessionId, request.TurnId, TurnLockTtl, ct))
        {
            logger.Warn(File, ServiceNames.App, "Bỏ qua lượt trùng, sessionId={SessionId}, turnId={TurnId}", request.SessionId, request.TurnId);
            return await ReplayAsync(request.SessionId, ct);
        }

        var session = await LoadSessionAsync(request.SessionId, ct);
        var state = await LoadStateAsync(request.SessionId, ct);

        // Chỉ phiên đang chạy mới nhận câu trả lời; phiên Created nghĩa là client chưa gọi start.
        EnsureRunning(session);

        if (state.State is InterviewState.Finished)
        {
            throw new InvalidOperationException("This interview has already finished.");
        }

        // Transcript rỗng = sinh viên im lặng: luật cứng xử lý, không gọi LLM.
        var timedOut = HardRules.IsTimedOut(state.Deadline, UtcNow);

        if (string.IsNullOrWhiteSpace(request.Transcript))
        {
            return await HandleSilenceAsync(session, state, request, ct);
        }

        var question = await LoadQuestionAsync(state, ct);
        var decision = await DecideAsync(question, state, request.Transcript, ct);
        var nextQuestionIndex = state.QuestionIndex + 1;
        var hasMoreQuestions = HardRules.NextQuestionState(nextQuestionIndex, state.QuestionIds.Count) == InterviewState.NextQuestion;

        // ENG-05: server áp luật cứng lên kết luận LLM.
        var next = HardRules.ResolveAfterLlm(
            decision.NeedFollowUp,
            state.FollowUpCount,
            session.MaxFollowUps,
            timedOut,
            hasMoreQuestions);

        var turn = await BuildTurnAsync(session, state, request, decision, ct);
        await publisher.PublishAsync(turn, ct);

        var view = await AdvanceAsync(session, state, next, decision, ct);

        logger.Info(File, ServiceNames.App,
            "Xử lý lượt trả lời, sessionId={SessionId}, turnId={TurnId}, next={NextState}, followUpCount={FollowUp}, timedOut={TimedOut}",
            request.SessionId, request.TurnId, next, state.FollowUpCount, timedOut);

        return new SubmitAnswerResult(view, decision);
    }

    public async Task<InterviewStateView> SkipAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, ct);
        var state = await LoadStateAsync(sessionId, ct);

        EnsureRunning(session);

        if (state.State is InterviewState.Finished)
        {
            throw new InvalidOperationException("This interview has already finished.");
        }

        // Bỏ qua câu hiện tại: ghi một lượt để log biết sinh viên đã skip (transcript rỗng).
        var turn = await BuildTurnAsync(session, state,
            new SubmitAnswerRequest(sessionId, Guid.NewGuid().ToString("N"), ""),
            new FollowUpDecision(false, null, "Sinh viên bỏ qua câu hỏi.", [], [], []), ct);
        await publisher.PublishAsync(turn, ct);

        var next = HardRules.NextQuestionState(state.QuestionIndex + 1, state.QuestionIds.Count);
        var view = await AdvanceAsync(session, state, next, null, ct);

        logger.Info(File, ServiceNames.App, "Bỏ qua câu, sessionId={SessionId}, questionIndex={Index}, next={NextState}",
            sessionId, state.QuestionIndex, next);

        return view;
    }

    public async Task FinishAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, ct);

        if (session.Status != SessionStatus.Completed && session.Status != SessionStatus.Abandoned)
        {
            session.Status = SessionStatus.Completed;
            session.CompletedAt = UtcNow;
            await sessions.UpdateAsync(session, ct);
        }

        // Xóa trạng thái đang chạy: phiên đã kết thúc, log nằm ở SQL.
        await states.DeleteAsync(sessionId.ToString(), ct);

        logger.Info(File, ServiceNames.App, "Kết thúc phiên thi, sessionId={SessionId}, status={Status}", sessionId, session.Status);
    }

    /// <summary>Sinh viên im lặng: nhắc một lần, hết lượt nhắc thì bỏ qua câu (ENG-05).</summary>
    private async Task<SubmitAnswerResult> HandleSilenceAsync(
        Data.Entities.InterviewSession session,
        SessionState state,
        SubmitAnswerRequest request,
        CancellationToken ct)
    {
        if (HardRules.OnSilence(state.SilencePrompts) == SilenceOutcome.PromptAgain)
        {
            // Nhắc lại KHÔNG phải một lượt hỏi/đáp: không publish, không tăng followUpCount.
            state.SilencePrompts++;
            await states.SaveAsync(session.Id.ToString(), state, ct);

            logger.Info(File, ServiceNames.App, "Sinh viên im lặng, nhắc lại một lần, sessionId={SessionId}, turnId={TurnId}",
                session.Id, request.TurnId);

            return new SubmitAnswerResult(ToView(session, state), null);
        }

        var turn = await BuildTurnAsync(session, state, request,
            new FollowUpDecision(false, null, "Sinh viên im lặng sau khi đã được nhắc.", [], [], []), ct);
        await publisher.PublishAsync(turn, ct);

        var next = HardRules.NextQuestionState(state.QuestionIndex + 1, state.QuestionIds.Count);
        var view = await AdvanceAsync(session, state, next, null, ct);

        logger.Warn(File, ServiceNames.App, "Sinh viên im lặng, bỏ qua câu, sessionId={SessionId}, turnId={TurnId}",
            session.Id, request.TurnId);

        return new SubmitAnswerResult(view, null);
    }

    /// <summary>Gọi LLM (nếu đã có) để lấy kết luận; thiếu <see cref="IFollowUpDecider"/> thì coi như đủ ý.</summary>
    private async Task<FollowUpDecision> DecideAsync(
        Data.Entities.Question question,
        SessionState state,
        string transcript,
        CancellationToken ct)
    {
        if (decider is null)
        {
            logger.Warn(File, ServiceNames.App, "IFollowUpDecider chưa được đăng ký (chờ QST-02), coi như đủ ý, sessionId={SessionId}",
                state.SessionId);
            return new FollowUpDecision(false, null, "Chưa có bộ quyết định hỏi xoáy, tạm coi như đủ ý.", [], [], []);
        }

        var context = await LoadContextAsync(question, transcript, ct);

        return await decider.DecideAsync(new FollowUpRequest(
            question.Content,
            question.ReferenceAnswer,
            question.KeyPoints.ToList(),
            transcript,
            state.FollowUpCount,
            _defaults.MaxFollowUps,
            context), ct);
    }

    /// <summary>Đoạn tài liệu làm căn cứ. Chưa có QST-01 thì trả rỗng — prompt vẫn chạy được.</summary>
    private async Task<IReadOnlyList<RetrievedChunk>> LoadContextAsync(
        Data.Entities.Question question,
        string transcript,
        CancellationToken ct)
    {
        if (retrieval is null)
        {
            return [];
        }

        try
        {
            var scope = new RetrievalScope(new[] { question.SourceDocumentId }.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToList());
            return await retrieval.SearchAsync(transcript, scope, 4, ct);
        }
        catch (Exception ex)
        {
            // Truy xuất lỗi không được làm hỏng lượt thi: chỉ mất phần trích dẫn nguồn.
            logger.Error(ex, File, ServiceNames.Qdrant, "Truy xuất ngữ cảnh thất bại, questionId={QuestionId}", question.Id);
            return [];
        }
    }

    /// <summary>Sau khi đã áp luật cứng: cập nhật state và chuyển sang câu tiếp theo nếu cần.</summary>
    private async Task<InterviewStateView> AdvanceAsync(
        Data.Entities.InterviewSession session,
        SessionState state,
        InterviewState next,
        FollowUpDecision? decision,
        CancellationToken ct)
    {
        switch (next)
        {
            case InterviewState.FollowUp:
                state.State = InterviewState.FollowUp;
                state.IsFollowUp = true;
                state.FollowUpCount++;
                state.SilencePrompts = 0;
                state.CurrentQuestionText = decision?.FollowUpQuestion;
                state.Deadline = UtcNow.AddSeconds(session.AnswerSeconds);
                state.TurnStartedAt = UtcNow;
                break;

            case InterviewState.NextQuestion:
            case InterviewState.AskQuestion:
                await MoveToNextQuestionAsync(session, state, ct);
                break;

            case InterviewState.Finished:
                // Kết thúc: đánh dấu phiên trong SQL rồi xóa state Redis.
                // KHÔNG lưu lại state sau đó (nếu lưu, phiên đã xong sẽ "sống lại" trong Redis).
                await FinishAsync(session.Id, ct);
                state.State = InterviewState.Finished;
                state.Deadline = null;
                state.SilencePrompts = 0;
                return ToView(session, state);
        }

        await states.SaveAsync(session.Id.ToString(), state, ct);
        return ToView(session, state);
    }

    private async Task MoveToNextQuestionAsync(Data.Entities.InterviewSession session, SessionState state, CancellationToken ct)
    {
        var index = state.QuestionIndex + 1;

        if (index >= state.QuestionIds.Count)
        {
            await FinishAsync(session.Id, ct);
            state.State = InterviewState.Finished;
            state.Deadline = null;
            state.SilencePrompts = 0;
            return;
        }

        var question = await questions.GetAsync(state.QuestionIds[index], ct)
                       ?? throw new InvalidOperationException($"Question {state.QuestionIds[index]} no longer exists.");

        state.State = InterviewState.AskQuestion;
        state.QuestionIndex = index;
        state.QuestionId = question.Id;
        state.CurrentQuestionText = question.Content;
        state.IsFollowUp = false;
        state.FollowUpCount = 0;
        state.SilencePrompts = 0;
        state.Deadline = UtcNow.AddSeconds(session.AnswerSeconds);
        state.TurnStartedAt = UtcNow;
    }

    private Task<TurnMessage> BuildTurnAsync(
        Data.Entities.InterviewSession session,
        SessionState state,
        SubmitAnswerRequest request,
        FollowUpDecision decision,
        CancellationToken ct)
    {
        var startedAt = state.TurnStartedAt ?? UtcNow;

        return Task.FromResult(new TurnMessage(
            TurnId: request.TurnId,
            SessionId: session.Id,
            QuestionId: state.QuestionId,
            Question: state.CurrentQuestionText ?? "",
            Transcript: request.Transcript,
            IsFollowUp: state.IsFollowUp,
            NeedFollowUp: decision.NeedFollowUp,
            Reason: decision.Reason,
            Sources: decision.Sources,
            StartedAt: startedAt,
            EndedAt: UtcNow));
    }

    /// <summary>Request trùng: trả lại trạng thái hiện tại thay vì xử lý lượt thứ hai.</summary>
    private async Task<SubmitAnswerResult> ReplayAsync(Guid sessionId, CancellationToken ct)
    {
        var view = await GetStateAsync(sessionId, ct)
                   ?? throw new InvalidOperationException($"No running state for session {sessionId}.");
        return new SubmitAnswerResult(view, null);
    }

    /// <summary>Chọn câu hỏi <c>Approved</c>, tránh lặp lại với các phiên gần đây (ENG-02).</summary>
    private async Task<IReadOnlyList<Guid>> PickQuestionsAsync(int count, CancellationToken ct)
    {
        var approved = (await questions.ListAsync(QuestionStatus.Approved, ct)).Select(q => q.Id).ToList();
        var recent = await usage.ListRecentAsync(RecentQuestionsWindow, ct);

        var selected = QuestionSelector.Select(approved, recent, count, Random.Shared);

        logger.Info(File, ServiceNames.App,
            "Chọn câu hỏi cho phiên, count={Count}, approved={Approved}, recentlyUsed={Recent}",
            count, approved.Count, recent.Count);

        return selected;
    }

    private async Task<Data.Entities.InterviewSession> LoadSessionAsync(Guid sessionId, CancellationToken ct)
        => await sessions.GetAsync(sessionId, ct) ?? throw new KeyNotFoundException($"Interview {sessionId} was not found.");

    /// <summary>Chỉ phiên <c>InProgress</c> mới nhận câu trả lời / skip. Phiên <c>Created</c> phải gọi start trước.</summary>
    private static void EnsureRunning(Data.Entities.InterviewSession session)
    {
        if (session.Status != SessionStatus.InProgress)
        {
            throw new InvalidOperationException(
                session.Status == SessionStatus.Created
                    ? "This interview has not started yet."
                    : $"This interview is already {session.Status}.");
        }
    }

    private async Task<SessionState> LoadStateAsync(Guid sessionId, CancellationToken ct)
        => await states.GetAsync(sessionId.ToString(), ct)
           ?? throw new InvalidOperationException("This interview is not running. Start it again.");

    private async Task<Data.Entities.Question> LoadQuestionAsync(SessionState state, CancellationToken ct)
    {
        if (state.QuestionId is not { } id)
        {
            throw new InvalidOperationException("The current question is missing.");
        }

        return await questions.GetAsync(id, ct)
               ?? throw new InvalidOperationException($"Question {id} no longer exists.");
    }

    private InterviewStateView ToView(Data.Entities.InterviewSession session, SessionState state) => new(
        SessionId: session.Id,
        State: state.State,
        QuestionIndex: state.QuestionIndex,
        QuestionCount: session.QuestionCount,
        CurrentQuestionText: state.CurrentQuestionText,
        IsFollowUp: state.IsFollowUp,
        FollowUpCount: state.FollowUpCount,
        MaxFollowUps: session.MaxFollowUps,
        RemainingSeconds: HardRules.RemainingSeconds(state.Deadline, UtcNow));
}
