using System.Collections.Concurrent;
using AiInterview.Common.Constants;
using AiInterview.Data.Models;

namespace AiInterview.Data.Repositories.Sessions;

/// <summary>
/// Fake session state (singleton): giữ trạng thái trong bộ nhớ thay cho Redis Hash <c>interview:{sessionId}</c>.
/// Hết TTL coi như mất phiên, giống hệt bản Redis để Service không phải biết đang chạy chế độ nào.
/// </summary>
public sealed class InMemorySessionStateRepository : ISessionStateRepository
{
    private readonly ConcurrentDictionary<string, Entry> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _locks = new();

    public Task<SessionState?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult<SessionState?>(null);
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            return Task.FromResult<SessionState?>(null);
        }

        // Hoạt động gia hạn TTL, giống lệnh EXPIRE ở bản Redis.
        entry.ExpiresAt = DateTime.UtcNow.Add(RedisKeys.InterviewTtl);
        return Task.FromResult<SessionState?>(Clone(entry.State));
    }

    public Task SaveAsync(string sessionId, SessionState state, CancellationToken ct = default)
    {
        state.SessionId = sessionId;
        _sessions[sessionId] = new Entry(Clone(state), DateTime.UtcNow.Add(RedisKeys.InterviewTtl));
        return Task.CompletedTask;
    }

    public Task<bool> TryLockAsync(string sessionId, string turnId, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = RedisKeys.Lock(sessionId, turnId);
        var now = DateTime.UtcNow;

        foreach (var stale in _locks.Where(l => l.Value <= now))
        {
            _locks.TryRemove(stale.Key, out _);
        }

        return Task.FromResult(_locks.TryAdd(key, now.Add(ttl)));
    }

    public Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <summary>Bản sao để người gọi sửa state mà không ảnh hưởng bản đã lưu (giống serialize qua Redis).</summary>
    private static SessionState Clone(SessionState source) => new()
    {
        SessionId = source.SessionId,
        State = source.State,
        QuestionIndex = source.QuestionIndex,
        QuestionId = source.QuestionId,
        FollowUpCount = source.FollowUpCount,
        Deadline = source.Deadline,
        QuestionIds = source.QuestionIds.ToList(),
        SilencePrompts = source.SilencePrompts,
        CurrentQuestionText = source.CurrentQuestionText,
        IsFollowUp = source.IsFollowUp,
        TurnStartedAt = source.TurnStartedAt
    };

    private sealed class Entry(SessionState state, DateTime expiresAt)
    {
        public SessionState State { get; } = state;

        public DateTime ExpiresAt { get; set; } = expiresAt;
    }
}
