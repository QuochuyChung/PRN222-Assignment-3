using AiInterview.DAL.Entities;
using AiInterview.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.DAL.Repositories.Questions;

public sealed class QuestionRepository(AppDbContext db) : IQuestionRepository
{
    public async Task AddAsync(Question question, CancellationToken ct = default)
    {
        db.Questions.Add(question);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<Question> questions, CancellationToken ct = default)
    {
        db.Questions.AddRange(questions);
        await db.SaveChangesAsync(ct);
    }

    public Task<Question?> GetAsync(Guid id, CancellationToken ct = default)
        => db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<Question>> ListAsync(QuestionStatus? status = null, CancellationToken ct = default)
    {
        var query = db.Questions.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(q => q.Status == status);
        }

        return await query.OrderByDescending(q => q.CreatedAt).ToListAsync(ct);
    }

    public Task<int> CountAsync(QuestionStatus? status = null, CancellationToken ct = default)
        => status is null
            ? db.Questions.CountAsync(ct)
            : db.Questions.CountAsync(q => q.Status == status, ct);

    public async Task UpdateAsync(Question question, CancellationToken ct = default)
    {
        question.UpdatedAt = DateTime.UtcNow;
        db.Questions.Update(question);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await db.Questions.Where(q => q.Id == id).ExecuteDeleteAsync(ct);
    }
}
