using System.Globalization;
using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AiInterview.DAL.Repositories.Sessions;

/// <summary>
/// Redis: sorted set <c>question-usage</c> với score là thời điểm dùng (Unix ms).
/// Dùng sorted set thay vì list để mỗi câu chỉ giữ một điểm và dọn câu cũ bằng <c>ZREMRANGEBYRANK</c>.
/// </summary>
public sealed class RedisQuestionUsageRepository(IConnectionMultiplexer redis, ILogger<RedisQuestionUsageRepository> logger)
    : IQuestionUsageRepository
{
    private const string File = "RedisQuestionUsageRepository.cs";
    private const string Key = "question-usage";

    /// <summary>Giữ tối đa ngần này câu gần nhất; đủ cho hàng trăm ca thi liền nhau.</summary>
    private const int MaxEntries = 500;

    private IDatabase Db => redis.GetDatabase();

    public async Task RecordAsync(Guid sessionId, IReadOnlyList<Guid> questionIds, CancellationToken ct = default)
    {
        if (questionIds.Count == 0)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var entries = questionIds
                .Select(id => new SortedSetEntry(id.ToString(), now))
                .ToArray();

            await Db.SortedSetAddAsync(Key, entries);

            // Giữ đúng MaxEntries câu mới nhất (rank 0 = score thấp nhất = cũ nhất).
            var count = await Db.SortedSetLengthAsync(Key);
            if (count > MaxEntries)
            {
                await Db.SortedSetRemoveRangeByRankAsync(Key, 0, count - MaxEntries - 1);
            }

            logger.Info(File, ServiceNames.Redis,
                "Ghi câu hỏi đã dùng, sessionId={SessionId}, count={Count}, key={Key}", sessionId, questionIds.Count, Key);
        }
        catch (Exception ex)
        {
            // Không được làm hỏng việc tạo phiên chỉ vì mất dấu "câu đã dùng".
            logger.Error(ex, File, ServiceNames.Redis, "Ghi câu hỏi đã dùng thất bại, sessionId={SessionId}", sessionId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Guid>> ListRecentAsync(int take, CancellationToken ct = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            var values = await Db.SortedSetRangeByRankAsync(Key, -take, -1, Order.Descending);
            return values
                .Select(v => Guid.TryParse(v.ToString(), out var id) ? id : (Guid?)null)
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "Đọc câu hỏi đã dùng thất bại, key={Key}", Key);
            throw;
        }
    }
}
