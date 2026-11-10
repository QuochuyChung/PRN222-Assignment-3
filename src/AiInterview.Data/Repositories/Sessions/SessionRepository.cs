using AiInterview.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Data.Repositories.Sessions;

public sealed class SessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task AddAsync(InterviewSession session, CancellationToken ct = default)
    {
        db.InterviewSessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public Task<InterviewSession?> GetAsync(Guid id, CancellationToken ct = default)
        => db.InterviewSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<InterviewSession>> ListAsync(SessionStatus? status = null, CancellationToken ct = default)
    {
        var query = db.InterviewSessions.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(s => s.Status == status);
        }

        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
    }

    public Task<int> CountAsync(SessionStatus? status = null, CancellationToken ct = default)
        => status is null
            ? db.InterviewSessions.CountAsync(ct)
            : db.InterviewSessions.CountAsync(s => s.Status == status, ct);

    public async Task UpdateAsync(InterviewSession session, CancellationToken ct = default)
    {
        db.InterviewSessions.Update(session);
        await db.SaveChangesAsync(ct);
    }
}
