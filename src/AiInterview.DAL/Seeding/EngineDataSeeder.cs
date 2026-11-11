using AiInterview.Common.Enums;
using AiInterview.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.DAL.Seeding;

/// <summary>
/// ENG-10: câu hỏi mẫu đã duyệt cho module Interview Engine, để tạo được phiên nhiều câu ở chế độ Fake.
/// Seeder của BASE chỉ có một câu <c>Approved</c> nên không đủ cho một phiên 3 câu.
/// </summary>
public sealed class EngineDataSeeder : IDataSeeder
{
    /// <summary>Chạy sau seeder của BASE (Order = 0) để không đụng dữ liệu mẫu của dashboard.</summary>
    public int Order => 10;

    private const string RazorPages = "Razor Pages khác MVC ở điểm nào?";

    public async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Questions
            .Where(q => q.Status == QuestionStatus.Approved)
            .Select(q => q.Content)
            .ToListAsync(ct);

        var missing = Samples().Where(s => !existing.Contains(s.Content)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        db.Questions.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    private static List<Question> Samples() =>
    [
        new()
        {
            Content = RazorPages,
            ReferenceAnswer = "Razor Pages tổ chức theo từng trang với PageModel, routing theo thư mục; MVC tách Controller, View, Model.",
            KeyPoints = new List<string> { "PageModel", "routing theo thư mục", "phù hợp ứng dụng nhỏ" },
            BloomLevel = BloomLevel.Understand,
            Status = QuestionStatus.Approved
        },
        new()
        {
            Content = "Dependency Injection giúp gì cho kiến trúc 3 lớp?",
            ReferenceAnswer = "Giảm phụ thuộc cứng giữa PageModel, Service và Repository, dễ thay bản Fake khi test.",
            KeyPoints = new List<string> { "giảm phụ thuộc", "dễ test", "thay thế implementation" },
            BloomLevel = BloomLevel.Apply,
            Status = QuestionStatus.Approved
        },
        new()
        {
            Content = "So sánh Scoped và Singleton trong ASP.NET Core.",
            ReferenceAnswer = "Scoped: một instance mỗi request; Singleton: một instance cho cả ứng dụng.",
            KeyPoints = new List<string> { "vòng đời", "DbContext là Scoped" },
            BloomLevel = BloomLevel.Analyze,
            Status = QuestionStatus.Approved
        },
        new()
        {
            Content = "Vì sao cần tách Repository khỏi Service?",
            ReferenceAnswer = "Để Service chỉ phụ thuộc interface, đổi SQL sang Mongo/Qdrant không phải sửa logic nghiệp vụ.",
            KeyPoints = new List<string> { "đảo ngược phụ thuộc", "đổi hạ tầng không sửa Service", "dễ viết Fake" },
            BloomLevel = BloomLevel.Analyze,
            Status = QuestionStatus.Approved
        },
        new()
        {
            Content = "Redis Streams khác Redis Pub/Sub ở điểm nào?",
            ReferenceAnswer = "Streams lưu message, có consumer group và ACK nên không mất dữ liệu; Pub/Sub chỉ đẩy cho client đang nghe.",
            KeyPoints = new List<string> { "lưu message", "consumer group", "ACK", "không mất dữ liệu" },
            BloomLevel = BloomLevel.Understand,
            Status = QuestionStatus.Approved
        },
        new()
        {
            Content = "RAG tự code gồm những bước nào?",
            ReferenceAnswer = "Trích text, chia chunk, embedding, lưu vector, truy xuất top-k rồi đưa vào prompt cho LLM.",
            KeyPoints = new List<string> { "chunking", "embedding", "vector search", "dựng prompt" },
            BloomLevel = BloomLevel.Remember,
            Status = QuestionStatus.Approved
        }
    ];
}
