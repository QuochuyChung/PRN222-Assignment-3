using AiInterview.Data.Repositories.Turns;
using AiInterview.Services.Interview;
using AiInterview.Services.Mapping;

namespace AiInterview.Services.TurnLog;

internal sealed class TurnLogService(ITurnRepository turns) : ITurnLogService
{
    public Task<bool> SaveAsync(TurnMessage turn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(turn);
        if (string.IsNullOrWhiteSpace(turn.TurnId) || turn.SessionId == Guid.Empty)
            throw new ArgumentException("TurnId and SessionId are required.", nameof(turn));
        return turns.InsertIfNotExistsAsync(turn.ToEntity(), ct);
    }

    public async Task<IReadOnlyList<TurnDto>> ListBySessionAsync(Guid sessionId, CancellationToken ct = default)
        => (await turns.ListBySessionAsync(sessionId, ct)).Select(t => t.ToDto()).ToList();

    public async Task<IReadOnlyList<TurnDto>> ListRecentAsync(int take, CancellationToken ct = default)
        => (await turns.ListRecentAsync(Math.Clamp(take, 1, 100), ct)).Select(t => t.ToDto()).ToList();
}
