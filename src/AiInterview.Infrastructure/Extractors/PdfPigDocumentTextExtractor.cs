using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Exceptions;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace AiInterview.Infrastructure.Extractors;

public class PdfPigDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly ILogger<PdfPigDocumentTextExtractor> _logger;

    public PdfPigDocumentTextExtractor(ILogger<PdfPigDocumentTextExtractor> logger)
    {
        _logger = logger;
    }

    public Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        content.CopyTo(memoryStream);
        byte[] pdfBytes = memoryStream.ToArray();

        var doc = new ExtractedDocument
        {
            FileName = fileName
        };

        try
        {
            using var pdf = PdfDocument.Open(pdfBytes);
            int totalTextLength = 0;

            foreach (var page in pdf.GetPages())
            {
                string text = page.Text?.Trim() ?? string.Empty;
                totalTextLength += text.Length;

                doc.Pages.Add(new ExtractedPage
                {
                    PageNumber = page.Number,
                    Content = text
                });
            }

            // PDF scan detection: If entire document contains zero or negligible text (< 10 chars)
            if (totalTextLength < 10)
            {
                _logger.LogWarning("PdfPigDocumentTextExtractor.cs - App: Phát hiện PDF scan hoặc không có lớp text, fileName={FileName}, totalTextLength={Length}",
                    fileName, totalTextLength);
                throw new ScannedPdfException("Tài liệu PDF là bản scan dạng hình ảnh hoặc không có lớp văn bản (text layer). Hệ thống yêu cầu tài liệu có thể trích xuất văn bản.");
            }

            _logger.LogInformation("PdfPigDocumentTextExtractor.cs - App: Trích xuất PDF thành công, fileName={FileName}, số trang={PageCount}, tổng ký tự={Chars}",
                fileName, doc.Pages.Count, totalTextLength);

            return Task.FromResult(doc);
        }
        catch (ScannedPdfException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfPigDocumentTextExtractor.cs - App: Lỗi khi đọc file PDF, fileName={FileName}", fileName);
            throw;
        }
    }
}

