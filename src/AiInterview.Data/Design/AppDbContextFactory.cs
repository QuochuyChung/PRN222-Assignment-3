using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiInterview.Data.Design;

/// <summary>
/// Dùng cho <c>dotnet ef</c> (tạo migration). Chỉ phục vụ lúc thiết kế, không dùng khi chạy app.
/// Đặt <c>ConnectionStrings__SqlServer</c> nếu muốn trỏ tới SQL Server khác khi chạy lệnh ef.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__SqlServer")
                               ?? "Server=localhost;Database=AiInterview;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
