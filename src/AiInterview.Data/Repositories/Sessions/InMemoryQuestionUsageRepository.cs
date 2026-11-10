using System.Collections.Concurrent;

namespace AiInterview.Data.Repositories.Sessions;

/// <summary>
/// Fake: giữ danh sách câu hỏi đã dùng trong bộ nhớ (singleton), mới nhất ở cuối.
/// Cùng ngữ nghĩa "mới nhất trước" với bản Redis để Service không phải biết chế độ chạy.
/// </summary>
public sealed class InMemoryQuestionUsageRepository : IQuestionUsageRepository
{
    private readonly ConcurrentDictionary<Guid, long> _used = new();
    private readonly object _gate = new();
    private long _sequence;
    private const int MaxEntries = 500;

    public Task RecordAsync(Guid sessionId, IReadOnlyList<Guid> questionIds, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var id in questionIds)
            {
                _used[id] = Interlocked.Increment(ref _sequence);
            }

            // Chỉ giữ những câu gần nhất để bộ nhớ không tăng mãi.
            if (_used.Count > MaxEntries)
            {
                foreach (var stale in _used.OrderByDescending(p => p.Value).Skip(MaxEntries).ToList())
                {
                    _used.TryRemove(stale.Key, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> ListRecentAsync(int take, CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyList<Guid> result = _used
                .OrderByDescending(p => p.Value)
                .Take(take)
                .Select(p => p.Key)
                .ToList();

            return Task.FromResult(result);
        }
    }
}
