using AiInterview.Common.Constants;
using AiInterview.Common.Logging;
using AiInterview.DAL.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AiInterview.DAL.Repositories.Queue;

/// <summary>
/// Redis Streams with consumer groups. Each read first claims messages that have been pending for too long
/// (<c>XAUTOCLAIM</c>: their consumer pod crashed or was replaced), then reads new ones (<c>XREADGROUP &gt;</c>).
/// </summary>
public sealed class RedisMessageQueueRepository(IConnectionMultiplexer redis, ILogger<RedisMessageQueueRepository> logger)
    : IMessageQueueRepository
{
    private const string File = "RedisMessageQueueRepository.cs";

    /// <summary>A pending message idle longer than this is treated as abandoned. Must be longer than the slowest document.</summary>
    private static readonly long ClaimIdleMs = (long)TimeSpan.FromMinutes(10).TotalMilliseconds;

    private IDatabase Db => redis.GetDatabase();

    public async Task EnsureGroupAsync(string stream, string group, CancellationToken ct = default)
    {
        try
        {
            await Db.StreamCreateConsumerGroupAsync(stream, group, StreamPosition.Beginning, createStream: true);
            logger.Info(File, ServiceNames.Redis, "Created consumer group, stream={Stream}, group={Group}", stream, group);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists.
        }
    }

    public async Task<string> PublishAsync(string stream, IReadOnlyDictionary<string, string> fields, CancellationToken ct = default)
    {
        try
        {
            var id = await Db.StreamAddAsync(stream, fields.Select(f => new NameValueEntry(f.Key, f.Value)).ToArray());
            logger.Info(File, ServiceNames.Redis, "XADD succeeded, stream={Stream}, messageId={MessageId}", stream, id.ToString());
            return id.ToString();
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "XADD failed, stream={Stream}", stream);
            throw;
        }
    }

    public async Task<IReadOnlyList<QueueMessage>> ReadAsync(string stream, string group, string consumer, int count, CancellationToken ct = default)
    {
        try
        {
            var claimed = await Db.StreamAutoClaimAsync(stream, group, consumer, ClaimIdleMs, "0-0", count);
            var entries = claimed.ClaimedEntries;
            if (entries.Length > 0)
            {
                logger.Warn(File, ServiceNames.Redis, "Reclaimed {Count} abandoned pending message(s), stream={Stream}, consumer={Consumer}",
                    entries.Length, stream, consumer);
            }
            else
            {
                entries = await Db.StreamReadGroupAsync(stream, group, consumer, StreamPosition.NewMessages, count);
            }

            return entries
                .Where(e => !e.IsNull)
                .Select(e => new QueueMessage(e.Id.ToString(), e.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString())))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "Read failed, stream={Stream}, group={Group}, consumer={Consumer}", stream, group, consumer);
            throw;
        }
    }

    public async Task AckAsync(string stream, string group, string messageId, CancellationToken ct = default)
    {
        try
        {
            await Db.StreamAcknowledgeAsync(stream, group, messageId);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.Redis, "XACK failed, stream={Stream}, group={Group}, messageId={MessageId}", stream, group, messageId);
            throw;
        }
    }

    public async Task DeadLetterAsync(string stream, string group, QueueMessage message, string reason, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string>(message.Fields)
        {
            ["originalId"] = message.Id,
            ["reason"] = reason,
            ["failedAt"] = DateTime.UtcNow.ToString("O")
        };
        // Write the dead-letter copy first; ACK the original only once the copy is stored.
        await PublishAsync(QueueNames.DeadLetter(stream), fields, ct);
        await AckAsync(stream, group, message.Id, ct);
        logger.Warn(File, ServiceNames.Redis, "Moved message to dead-letter, stream={Stream}, messageId={MessageId}, reason={Reason}",
            stream, message.Id, reason);
    }
}
