using System.Collections.Generic;
using AiInterview.Core.Models;

namespace AiInterview.Core.Interfaces;

public interface IChunker
{
    IReadOnlyList<ChunkDraft> Chunk(ExtractedDocument doc);
}

