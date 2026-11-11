using AiInterview.Common.Enums;
using AiInterview.BLL.Interview;

namespace AiInterview.Tests.Engine;

public class HardRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, 0)]
    [InlineData(60d, 60)]
    [InlineData(0.5d, 0)]
    [InlineData(-30d, 0)]
    public void RemainingSeconds_Is_Clamped_To_Zero_And_Rounded_Down(double? offsetSeconds, int expected)
    {
        DateTime? deadline = offsetSeconds is null ? null : Now.AddSeconds(offsetSeconds.Value);

        Assert.Equal(expected, HardRules.RemainingSeconds(deadline, Now));
    }

    [Fact]
    public void RemainingSeconds_Returns_Zero_For_A_Null_Deadline()
        => Assert.Equal(0, HardRules.RemainingSeconds(null, Now));

    [Theory]
    [InlineData(0, true)]     // vừa chạm deadline
    [InlineData(1, false)]    // còn 1 giây
    [InlineData(120, false)]  // còn 2 phút
    [InlineData(-30, true)]   // đã quá hạn
    public void IsTimedOut_Uses_The_Server_Side_Deadline_Only(int offsetSeconds, bool expected)
        => Assert.Equal(expected, HardRules.IsTimedOut(Now.AddSeconds(offsetSeconds), Now));

    [Fact]
    public void Missing_Deadline_Is_Never_Timed_Out()
        => Assert.False(HardRules.IsTimedOut(null, Now));

    [Theory]
    [InlineData(0, 2, false)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData(3, 2, true)]
    [InlineData(0, 0, true)]
    public void IsFollowUpExhausted_Compares_Against_The_Limit(int used, int max, bool expected)
        => Assert.Equal(expected, HardRules.IsFollowUpExhausted(used, max));

    [Fact]
    public void Silence_Prompts_Once_Then_Skips()
    {
        Assert.Equal(SilenceOutcome.PromptAgain, HardRules.OnSilence(0));
        Assert.Equal(SilenceOutcome.SkipQuestion, HardRules.OnSilence(1));
        Assert.Equal(SilenceOutcome.SkipQuestion, HardRules.OnSilence(5));
    }

    [Theory]
    // LLM muốn hỏi xoáy, còn lượt, chưa hết giờ ⇒ hỏi xoáy
    [InlineData(true, 0, 2, false, true, InterviewState.FollowUp)]
    [InlineData(true, 1, 2, false, true, InterviewState.FollowUp)]
    // Hết lượt xoáy ⇒ LLM bị ghi đè, chuyển câu (còn câu) hoặc kết thúc (hết câu)
    [InlineData(true, 2, 2, false, true, InterviewState.NextQuestion)]
    [InlineData(true, 2, 2, false, false, InterviewState.Finished)]
    // Hết giờ ⇒ không hỏi xoáy nữa dù còn lượt
    [InlineData(true, 0, 2, true, true, InterviewState.NextQuestion)]
    [InlineData(true, 0, 2, true, false, InterviewState.Finished)]
    // LLM nói đủ ý ⇒ chuyển câu / kết thúc
    [InlineData(false, 0, 2, false, true, InterviewState.NextQuestion)]
    [InlineData(false, 0, 2, false, false, InterviewState.Finished)]
    public void ResolveAfterLlm_Hard_Rules_Always_Beat_The_Llm(
        bool llmWantsFollowUp, int used, int max, bool timedOut, bool hasMore, InterviewState expected)
        => Assert.Equal(expected, HardRules.ResolveAfterLlm(llmWantsFollowUp, used, max, timedOut, hasMore));

    [Theory]
    [InlineData(0, 3, InterviewState.NextQuestion)]
    [InlineData(2, 3, InterviewState.NextQuestion)]
    [InlineData(3, 3, InterviewState.Finished)]
    [InlineData(4, 3, InterviewState.Finished)]
    public void NextQuestionState_Finishes_After_The_Last_Question(int nextIndex, int count, InterviewState expected)
        => Assert.Equal(expected, HardRules.NextQuestionState(nextIndex, count));

    [Fact]
    public void MaxSilencePrompts_Is_One()
        => Assert.Equal(1, HardRules.MaxSilencePrompts);
}
