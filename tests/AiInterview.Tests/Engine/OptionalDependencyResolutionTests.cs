using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiInterview.Tests.Engine;

/// <summary>
/// Kiểm tra hành vi DI của container mặc định: tham số constructor có giá trị mặc định
/// vẫn resolve được khi service tương ứng chưa được đăng ký (dùng cho ENG-04 khi chờ QST-02).
/// </summary>
public class OptionalDependencyResolutionTests
{
    private interface IMissing
    {
    }

    private sealed class UsesOptional(IMissing? missing = null)
    {
        public IMissing? Missing { get; } = missing;
    }

    private sealed class UsesRequired(IMissing missing)
    {
        public IMissing Missing { get; } = missing;
    }

    [Fact]
    public void Missing_Optional_Constructor_Dependency_Resolves_To_Default()
    {
        var provider = new ServiceCollection().AddTransient<UsesOptional>().BuildServiceProvider();

        var instance = provider.GetRequiredService<UsesOptional>();

        Assert.Null(instance.Missing);
    }

    [Fact]
    public void Missing_Required_Constructor_Dependency_Throws()
    {
        var provider = new ServiceCollection().AddTransient<UsesRequired>().BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<UsesRequired>());
    }

    [Fact]
    public void Optional_Dependency_Is_Used_When_It_Is_Registered()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IMissing, Missing>()
            .AddTransient<UsesOptional>()
            .BuildServiceProvider();

        var instance = provider.GetRequiredService<UsesOptional>();

        Assert.IsType<Missing>(instance.Missing);
    }

    private sealed class Missing : IMissing
    {
    }
}
