using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using AiInterview.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace AiInterview.Infrastructure.Queue;

public class RedisDocumentStreamQueue : IDocumentStreamPublisher, IDocumentStreamConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisDocumentStreamQueue> _logger;
    private readonly string _consumerName;
    private bool _groupInitialized;
    private bool _pendingDrained;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public RedisDocumentStreamQueue(
        IConnectionMultiplexer redis,
        IOptions<AiInterviewOptions> options,
        ILogger<RedisDocumentStreamQueue> logger)
    {
        _redis = redis;
        _options = options.Value.Redis ?? new RedisOptions();
        _logger = logger;
        _consumerName = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName;
    }

    private async Task EnsureGroupExistsAsync()
    {
        if (_groupInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_groupInitialized) return;

            var db = _redis.GetDatabase();
            string streamKey = _options.DocsStreamKey;
            string groupName = _options.DocsConsumerGroup;

            try
            {
                // Create consumer group if it doesn't already exist
                await db.StreamCreateConsumerGroupAsync(streamKey, groupName, StreamPosition.Beginning, createStream: true);
                _logger.LogInformation("RedisDocumentStreamQueue.cs - Redis: Created consumer group, stream={Stream}, group={Group}",
                    streamKey, groupName);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
            {
                // Consumer group already exists
            }

            _groupInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task PublishAsync(DocStreamMessage message, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        string streamKey = _options.DocsStreamKey;

        try
        {
            var entries = new[]
            {
                new NameValueEntry("documentId", message.DocumentId),
                new NameValueEntry("storageKey", message.StorageKey)
            };

            var id = await db.StreamAddAsync(streamKey, entries);

            _logger.LogInformation("RedisDocumentStreamQueue.cs - Redis: XADD succeeded, stream={Stream}, id={Id}, documentId={DocId}",
                streamKey, id, message.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RedisDocumentStreamQueue.cs - Redis: XADD failed, stream={Stream}, documentId={DocId}",
                streamKey, message.DocumentId);
            throw;
        }
    }

    public async Task<IReadOnlyList<DocStreamMessageEnvelope>> ReadMessagesAsync(int count, TimeSpan waitTimeout, CancellationToken ct = default)
    {
        await EnsureGroupExistsAsync();

        var db = _redis.GetDatabase();
        string streamKey = _options.DocsStreamKey;
        string groupName = _options.DocsConsumerGroup;

        try
        {
            // On startup, first re-read messages that were delivered to this consumer
            // but never acknowledged (e.g. the pod crashed mid-processing).
            // Reading from "0" returns this consumer's pending entries; once that is empty
            // we switch to ">" (new messages only).
            StreamEntry[] entries;
            if (!_pendingDrained)
            {
                entries = await db.StreamReadGroupAsync(streamKey, groupName, _consumerName, StreamPosition.Beginning, count);
                if (entries == null || entries.Length == 0)
                {
                    _pendingDrained = true;
                    entries = await db.StreamReadGroupAsync(streamKey, groupName, _consumerName, StreamPosition.NewMessages, count);
                }
                else
                {
                    _logger.LogInformation("RedisDocumentStreamQueue.cs - Redis: Recovered pending messages, stream={Stream}, count={Count}",
                        streamKey, entries.Length);
                }
            }
            else
            {
                entries = await db.StreamReadGroupAsync(streamKey, groupName, _consumerName, StreamPosition.NewMessages, count);
            }

            var list = new List<DocStreamMessageEnvelope>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    string docId = entry["documentId"].ToString() ?? string.Empty;
                    string storageKey = entry["storageKey"].ToString() ?? string.Empty;

                    list.Add(new DocStreamMessageEnvelope
                    {
                        MessageId = entry.Id.ToString(),
                        Message = new DocStreamMessage
                        {
                            DocumentId = docId,
                            StorageKey = storageKey
                        },
                        DeliveryCount = 1
                    });
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RedisDocumentStreamQueue.cs - Redis: Stream read failed, stream={Stream}, group={Group}",
                streamKey, groupName);
            return Array.Empty<DocStreamMessageEnvelope>();
        }
    }

    public async Task AcknowledgeAsync(string messageId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        string streamKey = _options.DocsStreamKey;
        string groupName = _options.DocsConsumerGroup;

        try
        {
            await db.StreamAcknowledgeAsync(streamKey, groupName, messageId);

            _logger.LogInformation("RedisDocumentStreamQueue.cs - Redis: XACK succeeded, stream={Stream}, group={Group}, messageId={MsgId}",
                streamKey, groupName, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RedisDocumentStreamQueue.cs - Redis: XACK failed, stream={Stream}, group={Group}, messageId={MsgId}",
                streamKey, groupName, messageId);
            throw;
        }
    }

    public async Task DeadLetterAsync(DocStreamMessageEnvelope envelope, string reason, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        string dlqKey = _options.DocsDeadLetterStreamKey;

        try
        {
            var entries = new[]
            {
                new NameValueEntry("documentId", envelope.Message.DocumentId),
                new NameValueEntry("storageKey", envelope.Message.StorageKey),
                new NameValueEntry("originalMessageId", envelope.MessageId),
                new NameValueEntry("reason", reason),
                new NameValueEntry("failedAt", DateTime.UtcNow.ToString("O"))
            };

            var id = await db.StreamAddAsync(dlqKey, entries);

            _logger.LogWarning("RedisDocumentStreamQueue.cs - Redis: Moved message to dead-letter stream, stream={Stream}, id={Id}, originalMessageId={MsgId}, documentId={DocId}",
                dlqKey, id, envelope.MessageId, envelope.Message.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RedisDocumentStreamQueue.cs - Redis: Failed to write dead-letter message, stream={Stream}, messageId={MsgId}",
                dlqKey, envelope.MessageId);
            throw;
        }

        // Only remove from the main stream once the dead-letter copy is safely stored.
        await AcknowledgeAsync(envelope.MessageId, ct);
    }
}
