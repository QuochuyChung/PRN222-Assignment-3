using AiInterview.Common.Enums;

namespace AiInterview.Services.Interview;

/// <summary>
/// ENG-05: luật cứng phía server — server quyết định, LLM chỉ góp ý.
/// Ba luật: (1) hết giờ thì không hỏi xoáy nữa, (2) hết lượt xoáy thì chuyển câu,
/// (3) im lặng thì nhắc một lần rồi bỏ qua câu. Không phụ thuộc repository nên test được thuần.
/// </summary>
internal static class HardRules
{
    /// <summary>Số lần nhắc tối đa khi sinh viên im lặng trước khi bỏ qua câu hỏi.</summary>
    public const int MaxSilencePrompts = 1;

    /// <summary>Thời gian còn lại (giây), làm tròn xuống và không bao giờ âm. Client nhận giá trị này.</summary>
    public static int RemainingSeconds(DateTime? deadlineUtc, DateTime nowUtc)
    {
        if (deadlineUtc is null)
        {
            return 0;
        }

        var remaining = (deadlineUtc.Value - nowUtc).TotalSeconds;
        return remaining <= 0 ? 0 : (int)Math.Floor(remaining);
    }

    /// <summary>Đã hết giờ trả lời chưa (theo deadline tuyệt đối do server giữ, không theo đồng hồ client).</summary>
    public static bool IsTimedOut(DateTime? deadlineUtc, DateTime nowUtc)
        => deadlineUtc is not null && nowUtc >= deadlineUtc.Value;

    /// <summary>Đã dùng hết số lượt hỏi xoáy của câu hiện tại chưa.</summary>
    public static bool IsFollowUpExhausted(int followUpCount, int maxFollowUps)
        => followUpCount >= maxFollowUps;

    /// <summary>
    /// Lượt im lặng: còn lượt nhắc thì nhắc lại (giữ nguyên câu, không gọi LLM), hết lượt nhắc thì bỏ qua câu.
    /// </summary>
    public static SilenceOutcome OnSilence(int silencePrompts)
        => silencePrompts < MaxSilencePrompts
            ? SilenceOutcome.PromptAgain
            : SilenceOutcome.SkipQuestion;

    /// <summary>
    /// Áp luật cứng lên kết luận của LLM. Luôn thắng LLM: hết giờ hoặc hết lượt xoáy thì chuyển câu
    /// dù <paramref name="llmWantsFollowUp"/> là true.
    /// </summary>
    public static InterviewState ResolveAfterLlm(
        bool llmWantsFollowUp,
        int followUpCount,
        int maxFollowUps,
        bool timedOut,
        bool hasMoreQuestions)
    {
        var mayFollowUp = llmWantsFollowUp
                          && !timedOut
                          && !IsFollowUpExhausted(followUpCount, maxFollowUps);

        return InterviewStateMachine.AfterEvaluation(mayFollowUp, hasMoreQuestions);
    }

    /// <summary>
    /// Câu mới có còn thời gian không: khi đã hết câu thì kết thúc, ngược lại mở câu tiếp theo.
    /// </summary>
    public static InterviewState NextQuestionState(int nextQuestionIndex, int questionCount)
        => nextQuestionIndex >= questionCount
            ? InterviewState.Finished
            : InterviewState.NextQuestion;
}

/// <summary>Kết quả xử lý một lượt im lặng.</summary>
internal enum SilenceOutcome
{
    /// <summary>Nhắc lại một lần, vẫn ở câu hiện tại.</summary>
    PromptAgain,

    /// <summary>Đã nhắc đủ số lần: bỏ qua câu và chuyển sang câu mới.</summary>
    SkipQuestion
}
