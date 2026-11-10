using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Models;

namespace AiInterview.Core.Interfaces;

public interface IDocumentStreamPublisher
{
    Task PublishAsync(DocStreamMessage message, CancellationToken ct = default);
}

public interface IDocumentStreamConsumer
{
    Task<IReadOnlyList<DocStreamMessageEnvelope>> ReadMessagesAsync(int count, TimeSpan waitTimeout, CancellationToken ct = default);
    Task AcknowledgeAsync(string messageId, CancellationToken ct = default);

    /// <summary>
    /// Moves a message that failed too many times to the dead-letter stream and removes it from the main stream.
    /// </summary>
    Task DeadLetterAsync(DocStreamMessageEnvelope envelope, string reason, CancellationToken ct = default);
}

