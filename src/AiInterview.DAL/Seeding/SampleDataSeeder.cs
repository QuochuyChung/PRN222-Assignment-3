using AiInterview.DAL.Entities;
using AiInterview.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.DAL.Seeding;

/// <summary>
/// Dữ liệu mẫu tối thiểu của BASE để Dashboard có số liệu khi chạy Fake.
/// Các module có seeder riêng; seeder này có thể xóa khi không cần.
/// </summary>
public sealed class SampleDataSeeder : IDataSeeder
{
    public int Order => 0;

    public async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Questions.AnyAsync(ct))
        {
            return;
        }

        var approved = new Question
        {
            Content = "Razor Pages khác MVC ở điểm nào?",
            ReferenceAnswer = "Razor Pages tổ chức theo từng trang với PageModel, routing theo thư mục; MVC tách Controller, View, Model.",
            KeyPoints = new List<string> { "PageModel", "routing theo thư mục", "phù hợp ứng dụng nhỏ" },
            BloomLevel = BloomLevel.Understand,
            Status = QuestionStatus.Approved
        };

        var drafts = new[]
        {
            new Question
            {
                Content = "Dependency Injection giúp gì cho kiến trúc 3 lớp?",
                ReferenceAnswer = "Giảm phụ thuộc cứng giữa PageModel, Service và Repository, dễ thay bản Fake khi test.",
                KeyPoints = new List<string> { "giảm phụ thuộc", "dễ test", "thay thế implementation" },
                BloomLevel = BloomLevel.Apply,
                Status = QuestionStatus.Draft
            },
            new Question
            {
                Content = "So sánh Scoped và Singleton trong ASP.NET Core.",
                ReferenceAnswer = "Scoped: một instance mỗi request; Singleton: một instance cho cả ứng dụng.",
                KeyPoints = new List<string> { "vòng đời", "DbContext là Scoped" },
                BloomLevel = BloomLevel.Analyze,
                Status = QuestionStatus.Draft
            }
        };

        db.Questions.Add(approved);
        db.Questions.AddRange(drafts);

        var session = new InterviewSession
        {
            Title = "Vấn đáp mẫu (dữ liệu giả)",
            StudentName = "Sinh viên mẫu",
            Status = SessionStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            StartedAt = DateTime.UtcNow.AddHours(-2),
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.InterviewSessions.Add(session);

        var start = DateTime.UtcNow.AddHours(-2);
        db.InterviewTurns.AddRange(
            new InterviewTurn
            {
                SessionId = session.Id,
                QuestionId = approved.Id,
                QuestionText = approved.Content,
                Transcript = "Razor Pages thì mỗi trang có một PageModel còn MVC thì có controller.",
                NeedFollowUp = true,
                Reason = "Chưa nêu được khi nào nên chọn Razor Pages.",
                StartedAt = start,
                EndedAt = start.AddSeconds(40)
            },
            new InterviewTurn
            {
                SessionId = session.Id,
                QuestionId = approved.Id,
                IsFollowUp = true,
                QuestionText = "Khi nào bạn chọn Razor Pages thay vì MVC?",
                Transcript = "Với ứng dụng nhỏ, nhiều trang độc lập và ít logic dùng chung.",
                NeedFollowUp = false,
                Reason = "Đã đủ ý chính.",
                StartedAt = start.AddSeconds(45),
                EndedAt = start.AddSeconds(80)
            });

        await db.SaveChangesAsync(ct);
    }
}
