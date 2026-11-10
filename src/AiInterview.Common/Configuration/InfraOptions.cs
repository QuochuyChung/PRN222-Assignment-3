using Microsoft.Extensions.Configuration;

namespace AiInterview.Common.Configuration;

/// <summary>Fake = chạy bằng dữ liệu giả trong bộ nhớ (máy dev). Real = nối hạ tầng thật (K8s).</summary>
public enum InfraMode
{
    Fake,
    Real
}

/// <summary>Vai trò của tiến trình: web (Razor Pages + SignalR), worker (xử lý nền) hoặc cả hai.</summary>
public enum AppRole
{
    Web,
    Worker,
    All
}

/// <summary>
/// Cấu hình chạy chung. Đọc từ section <c>Infra</c> (<c>Infra:Mode</c>, <c>Infra:Role</c>);
/// biến môi trường <c>ROLE</c> (nếu có) ghi đè <c>Infra:Role</c>.
/// </summary>
public sealed class InfraOptions
{
    public const string SectionName = "Infra";

    public InfraMode Mode { get; set; } = InfraMode.Fake;

    public AppRole Role { get; set; } = AppRole.All;

    public bool IsFake => Mode == InfraMode.Fake;

    public bool IsReal => Mode == InfraMode.Real;

    public bool RunsWeb => Role is AppRole.Web or AppRole.All;

    public bool RunsWorker => Role is AppRole.Worker or AppRole.All;

    public static InfraOptions From(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<InfraOptions>() ?? new InfraOptions();

        var role = configuration["ROLE"];
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<AppRole>(role, ignoreCase: true, out var parsed))
        {
            options.Role = parsed;
        }

        return options;
    }
}
