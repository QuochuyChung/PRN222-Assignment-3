using AiInterview.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiInterview.Data.Repositories.Turns;

public sealed class TurnRepository(AppDbContext db) : ITurnRepository
{
    public async Task<bool> InsertIfNotExistsAsync(InterviewTurn turn, CancellationToken ct = default)
    {
        if (await db.InterviewTurns.AnyAsync(t => t.TurnId == turn.TurnId, ct))
        {
            return false;
        }

        db.InterviewTurns.Add(turn);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Hai worker ghi cùng TurnId một lúc: bản kia thắng, coi như đã tồn tại.
            db.Entry(turn).State = EntityState.Detached;
            if (await db.InterviewTurns.AnyAsync(t => t.TurnId == turn.TurnId, ct))
            {
                return false;
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<InterviewTurn>> ListBySessionAsync(Guid sessionId, CancellationToken ct = default)
        => await db.InterviewTurns.AsNoTracking()
            .Where(t => t.SessionId == sessionId)
            .OrderBy(t => t.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<InterviewTurn>> ListRecentAsync(int take, CancellationToken ct = default)
        => await db.InterviewTurns.AsNoTracking()
            .OrderByDescending(t => t.EndedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => db.InterviewTurns.CountAsync(ct);
}
