using AiInterview.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace AiInterview.Tests.Common;

public class InfraOptionsTests
{
    private static IConfiguration Build(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Defaults_To_Fake_And_All()
    {
        var options = InfraOptions.From(Build(new()));

        Assert.True(options.IsFake);
        Assert.True(options.RunsWeb);
        Assert.True(options.RunsWorker);
    }

    [Fact]
    public void Reads_Mode_And_Role_From_Infra_Section()
    {
        var options = InfraOptions.From(Build(new()
        {
            ["Infra:Mode"] = "Real",
            ["Infra:Role"] = "Worker"
        }));

        Assert.True(options.IsReal);
        Assert.False(options.RunsWeb);
        Assert.True(options.RunsWorker);
    }

    [Fact]
    public void ROLE_Variable_Overrides_Infra_Role()
    {
        var options = InfraOptions.From(Build(new()
        {
            ["Infra:Role"] = "All",
            ["ROLE"] = "web"
        }));

        Assert.Equal(AppRole.Web, options.Role);
        Assert.True(options.RunsWeb);
        Assert.False(options.RunsWorker);
    }

    [Fact]
    public void Invalid_ROLE_Is_Ignored()
    {
        var options = InfraOptions.From(Build(new() { ["ROLE"] = "khong-hop-le" }));

        Assert.Equal(AppRole.All, options.Role);
    }
}
