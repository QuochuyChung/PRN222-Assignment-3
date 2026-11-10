using AiInterview.Common.Enums;

namespace AiInterview.Services.Interview;

/// <summary>
/// Máy trạng thái của một phiên thi (tasks.md §1.3, ENG-03):
/// <c>AskQuestion → Listening → Evaluating → FollowUp | NextQuestion | Finished</c>.
/// Chỉ kiểm tra tính hợp lệ của chuyển trạng thái; luật cứng (hết giờ, hết lượt xoáy) nằm ở ENG-05.
/// </summary>
internal static class InterviewStateMachine
{
    /// <summary>Các cạnh hợp lệ. <see cref="InterviewState.Finished"/> là trạng thái cuối, không có cạnh ra.</summary>
    private static readonly Dictionary<InterviewState, InterviewState[]> Allowed = new()
    {
        // AI đang đọc câu hỏi; đọc xong (hoặc bỏ qua) thì mở mic.
        [InterviewState.AskQuestion] = [InterviewState.Listening, InterviewState.Finished],

        // Sinh viên đang trả lời; có transcript hoặc hết giờ thì chuyển sang đánh giá.
        [InterviewState.Listening] = [InterviewState.Evaluating, InterviewState.Finished],

        // Đã có kết luận của LLM (hoặc luật cứng ép): hỏi xoáy, sang câu mới, hoặc kết thúc.
        [InterviewState.Evaluating] = [InterviewState.FollowUp, InterviewState.NextQuestion, InterviewState.Finished],

        // AI đọc câu hỏi xoáy; đọc xong thì lại mở mic cho chính câu hỏi đó.
        [InterviewState.FollowUp] = [InterviewState.Listening, InterviewState.Finished],

        // Chuẩn bị câu hỏi chính kế tiếp (hoặc hết câu thì kết thúc).
        [InterviewState.NextQuestion] = [InterviewState.AskQuestion, InterviewState.Finished],

        [InterviewState.Finished] = []
    };

    /// <summary>Các trạng thái có thể đi tới từ <paramref name="from"/>.</summary>
    public static IReadOnlyList<InterviewState> AllowedFrom(InterviewState from)
        => Allowed.TryGetValue(from, out var targets) ? targets : [];

    public static bool CanTransition(InterviewState from, InterviewState to)
        => AllowedFrom(from).Contains(to);

    /// <summary>Kiểm tra rồi trả về trạng thái mới; ném <see cref="InvalidOperationException"/> nếu cạnh không hợp lệ.</summary>
    public static InterviewState Transition(InterviewState from, InterviewState to)
        => CanTransition(from, to)
            ? to
            : throw new InvalidOperationException($"Chuyển trạng thái không hợp lệ: {from} → {to}.");

    /// <summary>
    /// Trạng thái kế tiếp sau khi đánh giá xong một lượt, khi server đã áp luật cứng.
    /// <paramref name="needsFollowUp"/> false ⇒ sang câu mới; <paramref name="hasMoreQuestions"/> false ⇒ kết thúc.
    /// Giữ đúng một chỗ quyết định thứ tự <c>FollowUp → NextQuestion → Finished</c>.
    /// </summary>
    public static InterviewState AfterEvaluation(bool needsFollowUp, bool hasMoreQuestions)
        => needsFollowUp
            ? InterviewState.FollowUp
            : hasMoreQuestions
                ? InterviewState.NextQuestion
                : InterviewState.Finished;
}
