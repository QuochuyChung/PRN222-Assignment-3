using System.Text;
using AiInterview.Common.Logging;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AiInterview.Services.Documents;

/// <summary>The file can never be processed (scanned PDF, broken file, wrong type). The worker must not retry it.</summary>
internal sealed class DocumentRejectedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// PDF via PdfPig (one page = one <see cref="ExtractedPage"/>), PPTX via OpenXml (one slide = one page),
/// DOCX via OpenXml (a new section starts at every Heading/Title paragraph).
/// </summary>
internal sealed class DocumentTextExtractor(ILogger<DocumentTextExtractor> logger) : IDocumentTextExtractor
{
    private const string File = "DocumentTextExtractor.cs";

    /// <summary>Fewer characters than this in the whole PDF means there is no text layer.</summary>
    private const int MinPdfChars = 20;

    public async Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!IngestionOptions.AllowedExtensions.Contains(ext))
            throw new DocumentRejectedException($"File type '{ext}' is not supported. Use .pdf, .docx or .pptx.");

        // OpenXml and PdfPig need a seekable stream.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        IReadOnlyList<ExtractedPage> pages;
        try
        {
            pages = ext switch
            {
                ".pdf" => ReadPdf(buffer.ToArray()),
                ".pptx" => ReadPptx(buffer),
                _ => ReadDocx(buffer)
            };
        }
        catch (Exception ex) when (ex is not DocumentRejectedException)
        {
            logger.Error(ex, File, ServiceNames.App, "Could not read file, fileName={FileName}", fileName);
            throw new DocumentRejectedException($"The file could not be read. It may be damaged or not a real {ext} file.", ex);
        }

        if (ext == ".pdf" && pages.Sum(p => p.Text.Length) < MinPdfChars)
        {
            logger.Warn(File, ServiceNames.App, "Scanned PDF without a text layer, fileName={FileName}, pages={Pages}", fileName, pages.Count);
            throw new DocumentRejectedException(
                "This PDF looks like a scan: it has no selectable text. Run OCR on it or upload a PDF that contains real text.");
        }

        logger.Info(File, ServiceNames.App, "Extracted text, fileName={FileName}, pages={Pages}, chars={Chars}",
            fileName, pages.Count, pages.Sum(p => p.Text.Length));
        return new ExtractedDocument(fileName, pages);
    }

    private static List<ExtractedPage> ReadPdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return pdf.GetPages().Select(p => new ExtractedPage(p.Number, ContentOrderTextExtractor.GetText(p).Trim())).ToList();
    }

    private static List<ExtractedPage> ReadPptx(Stream stream)
    {
        using var deck = PresentationDocument.Open(stream, false);
        var part = deck.PresentationPart;
        var slideIds = part?.Presentation?.SlideIdList?.Elements<P.SlideId>() ?? [];

        var pages = new List<ExtractedPage>();
        foreach (var slideId in slideIds)
        {
            var slide = (SlidePart)part!.GetPartById(slideId.RelationshipId!);
            var lines = slide.Slide?.Descendants<A.Paragraph>().Select(p => p.InnerText.Trim()).Where(t => t.Length > 0) ?? [];
            pages.Add(new ExtractedPage(pages.Count + 1, string.Join('\n', lines)));
        }
        return pages;
    }

    private static List<ExtractedPage> ReadDocx(Stream stream)
    {
        using var word = WordprocessingDocument.Open(stream, false);
        var paragraphs = word.MainDocumentPart?.Document?.Body?.Descendants<W.Paragraph>() ?? [];

        var pages = new List<ExtractedPage>();
        var section = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var text = paragraph.InnerText.Trim();
            if (text.Length == 0) continue;

            var style = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
            var isHeading = style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) || style.Equals("Title", StringComparison.OrdinalIgnoreCase);
            if (isHeading && section.Length > 0)
            {
                pages.Add(new ExtractedPage(pages.Count + 1, section.ToString().Trim()));
                section.Clear();
            }
            section.AppendLine(text);
        }

        if (section.Length > 0) pages.Add(new ExtractedPage(pages.Count + 1, section.ToString().Trim()));
        return pages;
    }
}
