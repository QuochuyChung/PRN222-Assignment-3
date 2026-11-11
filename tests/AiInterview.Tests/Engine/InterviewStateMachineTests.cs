using AiInterview.Common.Enums;
using AiInterview.BLL.Interview;

namespace AiInterview.Tests.Engine;

public class InterviewStateMachineTests
{
    /// <summary>Bảng chuyển trạng thái đầy đủ theo tasks.md §1.3 — mọi cặp (from, to) đều được kiểm.</summary>
    public static TheoryData<InterviewState, InterviewState, bool> Table => new()
    {
        // AskQuestion
        { InterviewState.AskQuestion, InterviewState.Listening, true },
        { InterviewState.AskQuestion, InterviewState.Finished, true },
        { InterviewState.AskQuestion, InterviewState.Evaluating, false },
        { InterviewState.AskQuestion, InterviewState.FollowUp, false },
        { InterviewState.AskQuestion, InterviewState.NextQuestion, false },
        { InterviewState.AskQuestion, InterviewState.AskQuestion, false },

        // Listening
        { InterviewState.Listening, InterviewState.Evaluating, true },
        { InterviewState.Listening, InterviewState.Finished, true },
        { InterviewState.Listening, InterviewState.AskQuestion, false },
        { InterviewState.Listening, InterviewState.FollowUp, false },
        { InterviewState.Listening, InterviewState.NextQuestion, false },
        { InterviewState.Listening, InterviewState.Listening, false },

        // Evaluating
        { InterviewState.Evaluating, InterviewState.FollowUp, true },
        { InterviewState.Evaluating, InterviewState.NextQuestion, true },
        { InterviewState.Evaluating, InterviewState.Finished, true },
        { InterviewState.Evaluating, InterviewState.AskQuestion, false },
        { InterviewState.Evaluating, InterviewState.Listening, false },
        { InterviewState.Evaluating, InterviewState.Evaluating, false },

        // FollowUp
        { InterviewState.FollowUp, InterviewState.Listening, true },
        { InterviewState.FollowUp, InterviewState.Finished, true },
        { InterviewState.FollowUp, InterviewState.AskQuestion, false },
        { InterviewState.FollowUp, InterviewState.Evaluating, false },
        { InterviewState.FollowUp, InterviewState.NextQuestion, false },
        { InterviewState.FollowUp, InterviewState.FollowUp, false },

        // NextQuestion
        { InterviewState.NextQuestion, InterviewState.AskQuestion, true },
        { InterviewState.NextQuestion, InterviewState.Finished, true },
        { InterviewState.NextQuestion, InterviewState.Listening, false },
        { InterviewState.NextQuestion, InterviewState.Evaluating, false },
        { InterviewState.NextQuestion, InterviewState.FollowUp, false },
        { InterviewState.NextQuestion, InterviewState.NextQuestion, false },

        // Finished là trạng thái cuối
        { InterviewState.Finished, InterviewState.AskQuestion, false },
        { InterviewState.Finished, InterviewState.Listening, false },
        { InterviewState.Finished, InterviewState.Evaluating, false },
        { InterviewState.Finished, InterviewState.FollowUp, false },
        { InterviewState.Finished, InterviewState.NextQuestion, false },
        { InterviewState.Finished, InterviewState.Finished, false }
    };

    [Theory]
    [MemberData(nameof(Table))]
    public void CanTransition_Matches_Table(InterviewState from, InterviewState to, bool expected)
        => Assert.Equal(expected, InterviewStateMachine.CanTransition(from, to));

    [Fact]
    public void The_Table_Covers_Every_State_Pair()
    {
        var states = Enum.GetValues<InterviewState>();
        Assert.Equal(states.Length * states.Length, Table.Count());
    }

    [Fact]
    public void Transition_Returns_Target_When_Allowed()
        => Assert.Equal(InterviewState.Listening, InterviewStateMachine.Transition(InterviewState.AskQuestion, InterviewState.Listening));

    [Fact]
    public void Transition_Throws_When_Not_Allowed()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => InterviewStateMachine.Transition(InterviewState.Listening, InterviewState.AskQuestion));

        Assert.Contains("Listening", ex.Message);
        Assert.Contains("AskQuestion", ex.Message);
    }

    [Theory]
    [InlineData(true, true, InterviewState.FollowUp)]
    [InlineData(true, false, InterviewState.FollowUp)]
    [InlineData(false, true, InterviewState.NextQuestion)]
    [InlineData(false, false, InterviewState.Finished)]
    public void AfterEvaluation_Prefers_FollowUp_Then_Next_Question_Then_Finished(
        bool needsFollowUp, bool hasMoreQuestions, InterviewState expected)
        => Assert.Equal(expected, InterviewStateMachine.AfterEvaluation(needsFollowUp, hasMoreQuestions));

    [Fact]
    public void Every_State_Is_Reachable_From_AskQuestion()
    {
        var seen = new HashSet<InterviewState> { InterviewState.AskQuestion };
        var queue = new Queue<InterviewState>([InterviewState.AskQuestion]);

        while (queue.Count > 0)
        {
            foreach (var next in InterviewStateMachine.AllowedFrom(queue.Dequeue()))
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        Assert.Equal(Enum.GetValues<InterviewState>().ToHashSet(), seen);
    }
}
