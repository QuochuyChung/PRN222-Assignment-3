using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Models;

namespace AiInterview.Core.Interfaces;

public interface IDocumentTextExtractor
{
    Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default);
}

