using System.Globalization;
using System.Text.Json;
using AiInterview.Common.Constants;
using AiInterview.Common.Enums;
using AiInterview.Common.Logging;
using AiInterview.DAL.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AiInterview.DAL.Repositories.Sessions;

/// <summary>
/// Redis Hash <c>interview:{sessionId}</c> (xem tasks.md, Phụ lục B): lưu <c>deadline</c> tuyệt đối và gia hạn TTL
/// mỗi lần đọc/ghi. Khóa chống trùng dùng <c>SET NX EX</c> trên <c>lock:{sessionId}:{turnId}</c>.
/// </summary>
public sealed class RedisSessionStateRepository(IConnectionMultiplexer redis, ILogger<RedisSessionStateRepository> logger)
    : ISessionStateRepository
{
    private const string File = "RedisSessionStateRepository.cs";

    private IDatabase Db => redis.GetDatabase();

    public async Task<SessionState?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        var key = RedisKeys.Interview(sessionId);

        try
        {
            var entries = await Db.HashGetAllAsync(key);
            if (entries.Length == 0)
            {
                logger.Info(File, ServiceNames.Redis, "Không tìm thấy trạng thái phiên (hết TTL hoặc chưa tạo), sessionId={SessionId}", sessionId);
                return null;
            }

            var map = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
            var state = Read(sessionId, map);

            // Gia hạn TTL theo hoạt động.
            await Db.KeyExpireAsync(key, RedisKeys.InterviewTtl);
            return state;
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "Đọc trạng thái phiên thất bại, sessionId={SessionId}", sessionId);
            throw;
        }
    }

    public async Task SaveAsync(string sessionId, SessionState state, CancellationToken ct = default)
    {
        var key = RedisKeys.Interview(sessionId);
        state.SessionId = sessionId;

        try
        {
            var entries = Write(state);
            await Db.HashSetAsync(key, entries);
            await Db.KeyExpireAsync(key, RedisKeys.InterviewTtl);

            logger.Info(File, ServiceNames.Redis,
                "Lưu trạng thái phiên, sessionId={SessionId}, state={State}, questionIndex={Index}, followUpCount={FollowUp}, deadline={Deadline}",
                sessionId, state.State, state.QuestionIndex, state.FollowUpCount, state.Deadline);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "Lưu trạng thái phiên thất bại, sessionId={SessionId}", sessionId);
            throw;
        }
    }

    public async Task<bool> TryLockAsync(string sessionId, string turnId, TimeSpan ttl, CancellationToken ct = default)
    {
        var key = RedisKeys.Lock(sessionId, turnId);

        try
        {
            var acquired = await Db.StringSetAsync(key, turnId, ttl, When.NotExists);
            if (!acquired)
            {
                logger.Warn(File, ServiceNames.Redis, "Bỏ qua request trùng, sessionId={SessionId}, turnId={TurnId}", sessionId, turnId);
            }

            return acquired;
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "Đặt khóa chống trùng thất bại, sessionId={SessionId}, turnId={TurnId}", sessionId, turnId);
            throw;
        }
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var key = RedisKeys.Interview(sessionId);

        try
        {
            await Db.KeyDeleteAsync(key);
            logger.Info(File, ServiceNames.Redis, "Xóa trạng thái phiên, sessionId={SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "Xóa trạng thái phiên thất bại, sessionId={SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>Field của Hash theo Phụ lục B; <c>questionIds</c> là field bổ sung để khôi phục danh sách câu hỏi.</summary>
    private static HashEntry[] Write(SessionState state) =>
    [
        new("state", state.State.ToString()),
        new("questionIndex", state.QuestionIndex.ToString(CultureInfo.InvariantCulture)),
        new("questionId", state.QuestionId?.ToString() ?? ""),
        new("followUpCount", state.FollowUpCount.ToString(CultureInfo.InvariantCulture)),
        new("deadline", state.Deadline?.ToString("O", CultureInfo.InvariantCulture) ?? ""),
        new("questionIds", JsonSerializer.Serialize(state.QuestionIds)),
        new("silencePrompts", state.SilencePrompts.ToString(CultureInfo.InvariantCulture)),
        new("currentQuestionText", state.CurrentQuestionText ?? ""),
        new("isFollowUp", state.IsFollowUp.ToString()),
        new("turnStartedAt", state.TurnStartedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "")
    ];

    private static SessionState Read(string sessionId, IReadOnlyDictionary<string, string> map)
    {
        string Get(string name) => map.TryGetValue(name, out var value) ? value : "";

        var questionIds = Get("questionIds");

        return new SessionState
        {
            SessionId = sessionId,
            State = Enum.TryParse<InterviewState>(Get("state"), out var parsed) ? parsed : InterviewState.AskQuestion,
            QuestionIndex = int.TryParse(Get("questionIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ? index : 0,
            QuestionId = Guid.TryParse(Get("questionId"), out var questionId) ? questionId : null,
            FollowUpCount = int.TryParse(Get("followUpCount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var followUp) ? followUp : 0,
            Deadline = DateTime.TryParse(Get("deadline"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var deadline)
                ? deadline
                : null,
            QuestionIds = string.IsNullOrWhiteSpace(questionIds)
                ? new List<Guid>()
                : JsonSerializer.Deserialize<List<Guid>>(questionIds) ?? new List<Guid>(),
            SilencePrompts = int.TryParse(Get("silencePrompts"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var silence) ? silence : 0,
            CurrentQuestionText = Get("currentQuestionText") is { Length: > 0 } text ? text : null,
            IsFollowUp = bool.TryParse(Get("isFollowUp"), out var isFollowUp) && isFollowUp,
            TurnStartedAt = DateTime.TryParse(Get("turnStartedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var turnStarted)
                ? turnStarted
                : null
        };
    }
}
