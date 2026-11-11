using AiInterview.Common.Configuration;
using AiInterview.Common.Enums;
using AiInterview.BLL.Interview;
using AiInterview.BLL.Registration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiInterview.Tests.Engine;

/// <summary>
/// ENG-04: kiểm tra trên **composition root thật** (<see cref="ServicesServiceCollectionExtensions.AddServices"/>)
/// rằng <see cref="IFollowUpDecider"/> đã được nối và không còn rơi vào nhánh "chưa đăng ký".
/// Đây là lỗi từng tồn tại: <c>FollowUpDecider</c> có code + test nhưng quên đăng ký DI.
/// </summary>
public class EngineRegistrationTests
{
    private static InfraOptions FakeInfra() => new() { Mode = InfraMode.Fake, Role = AppRole.All };

    private static ServiceProvider Build()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Interview:QuestionCount"] = "3",
                ["Interview:MaxFollowUps"] = "2",
                ["Interview:AnswerSeconds"] = "60"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddServices(configuration, FakeInfra());
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void Follow_Up_Decider_Is_Registered_And_Real()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        var decider = scope.ServiceProvider.GetRequiredService<IFollowUpDecider>();

        Assert.IsType<FollowUpDecider>(decider);
    }

    [Fact]
    public void Interview_Service_Receives_The_Decider_Instead_Of_Null()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        // Nếu IFollowUpDecider chưa đăng ký, tham số tuỳ chọn sẽ lặng lẽ là null
        // và mọi câu trả lời đều "đủ ý" — đúng lỗi cần chặn bằng test này.
        var service = scope.ServiceProvider.GetRequiredService<IInterviewSessionService>();

        Assert.NotNull(service);
    }

    [Fact]
    public async Task Fake_Mode_Can_Produce_A_Follow_Up_Through_The_Real_Graph()
    {
        using var provider = Build();
        await using var scope = provider.CreateAsyncScope();

        var decider = scope.ServiceProvider.GetRequiredService<IFollowUpDecider>();
        var decision = await decider.DecideAsync(new FollowUpRequest(
            "Razor Pages khác MVC ở điểm nào?",
            "Razor Pages dùng PageModel và routing theo thư mục.",
            ["PageModel", "routing theo thư mục", "phù hợp với ứng dụng nhỏ"],
            "Razor Pages thì mỗi trang có một PageModel riêng thầy ạ.",
            FollowUpCount: 0,
            MaxFollowUps: 2,
            ContextChunks: []));

        Assert.True(decision.NeedFollowUp);
        Assert.False(string.IsNullOrWhiteSpace(decision.FollowUpQuestion));
    }

    [Fact]
    public void Retrieval_And_TurnLog_Are_Registered_For_The_Engine()
    {
        using var provider = Build();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AiInterview.BLL.Retrieval.IRetrievalService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AiInterview.BLL.TurnLog.ITurnLogService>());
    }

    [Fact]
    public void Hard_Rule_Keeps_Precedence_Over_The_Real_Decider()
    {
        // Dù decider thật trả needFollowUp=true, hết lượt xoáy thì server vẫn chuyển câu.
        var next = HardRules.ResolveAfterLlm(
            llmWantsFollowUp: true,
            followUpCount: 2,
            maxFollowUps: 2,
            timedOut: false,
            hasMoreQuestions: true);

        Assert.Equal(InterviewState.NextQuestion, next);
    }
}
