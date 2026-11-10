using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Extractors;

public class CompositeDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly OpenXmlDocumentTextExtractor _openXmlExtractor;
    private readonly PdfPigDocumentTextExtractor _pdfPigExtractor;
    private readonly ILogger<CompositeDocumentTextExtractor> _logger;

    public CompositeDocumentTextExtractor(
        OpenXmlDocumentTextExtractor openXmlExtractor,
        PdfPigDocumentTextExtractor pdfPigExtractor,
        ILogger<CompositeDocumentTextExtractor> logger)
    {
        _openXmlExtractor = openXmlExtractor;
        _pdfPigExtractor = pdfPigExtractor;
        _logger = logger;
    }

    public async Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();

        switch (ext)
        {
            case ".pdf":
                return await _pdfPigExtractor.ExtractAsync(content, fileName, ct);

            case ".docx":
            case ".pptx":
                return await _openXmlExtractor.ExtractAsync(content, fileName, ct);

            default:
                _logger.LogError("CompositeDocumentTextExtractor.cs - App: Định dạng file không được hỗ trợ, fileName={FileName}, extension={Ext}",
                    fileName, ext);
                throw new NotSupportedException($"Định dạng file '{ext}' không được hỗ trợ. Chỉ hỗ trợ .pdf, .docx, .pptx.");
        }
    }
}

