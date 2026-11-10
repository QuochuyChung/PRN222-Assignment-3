using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AiInterview.Core.Interfaces;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}

