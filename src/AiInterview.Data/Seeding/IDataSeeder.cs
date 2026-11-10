namespace AiInterview.Data.Seeding;

/// <summary>
/// Seed dữ liệu SQL khi chạy chế độ Fake (mỗi lần khởi động DB in-memory là rỗng).
/// Mỗi người viết seeder của module mình (QST-06, ENG-10...) và đăng ký trong file Registration của module.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Seeder có Order nhỏ chạy trước.</summary>
    int Order { get; }

    Task SeedAsync(AppDbContext db, CancellationToken ct = default);
}
