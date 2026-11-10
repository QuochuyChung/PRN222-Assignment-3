using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;

namespace AiInterview.Infrastructure.Extractors;

public class OpenXmlDocumentTextExtractor : IDocumentTextExtractor
{
    private readonly ILogger<OpenXmlDocumentTextExtractor> _logger;

    public OpenXmlDocumentTextExtractor(ILogger<OpenXmlDocumentTextExtractor> logger)
    {
        _logger = logger;
    }

    public Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".docx")
        {
            return Task.FromResult(ExtractDocx(content, fileName));
        }
        else if (ext == ".pptx")
        {
            return Task.FromResult(ExtractPptx(content, fileName));
        }

        throw new NotSupportedException($"OpenXML extractor does not support extension: {ext}");
    }

    private ExtractedDocument ExtractDocx(Stream content, string fileName)
    {
        var doc = new ExtractedDocument
        {
            FileName = fileName
        };

        // Ensure stream is copyable if needed
        using var memoryStream = new MemoryStream();
        content.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            _logger.LogWarning("OpenXmlDocumentTextExtractor.cs - App: File Word không có phần thân (body), fileName={FileName}", fileName);
            return doc;
        }

        var sb = new StringBuilder();
        int pageNumber = 1;
        var paragraphs = body.Elements<Paragraph>().ToList();

        foreach (var paragraph in paragraphs)
        {
            string text = paragraph.InnerText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) continue;

            sb.AppendLine(text);

            // Group roughly every ~2000 characters or section break as a logical page for DOCX
            if (sb.Length >= 2000)
            {
                doc.Pages.Add(new ExtractedPage
                {
                    PageNumber = pageNumber++,
                    Content = sb.ToString().Trim()
                });
                sb.Clear();
            }
        }

        if (sb.Length > 0)
        {
            doc.Pages.Add(new ExtractedPage
            {
                PageNumber = pageNumber,
                Content = sb.ToString().Trim()
            });
        }

        _logger.LogInformation("OpenXmlDocumentTextExtractor.cs - App: Trích xuất Word thành công, fileName={FileName}, số phần/trang={PageCount}",
            fileName, doc.Pages.Count);

        return doc;
    }

    private ExtractedDocument ExtractPptx(Stream content, string fileName)
    {
        var doc = new ExtractedDocument
        {
            FileName = fileName
        };

        using var memoryStream = new MemoryStream();
        content.CopyTo(memoryStream);
        memoryStream.Position = 0;

        using var presentationDoc = PresentationDocument.Open(memoryStream, false);
        var presentationPart = presentationDoc.PresentationPart;
        if (presentationPart == null || presentationPart.Presentation == null)
        {
            _logger.LogWarning("OpenXmlDocumentTextExtractor.cs - App: File PowerPoint không có PresentationPart, fileName={FileName}", fileName);
            return doc;
        }

        var slideIdList = presentationPart.Presentation.SlideIdList;
        if (slideIdList == null) return doc;

        int slideIndex = 1;
        foreach (var slideId in slideIdList.ChildElements.OfType<SlideId>())
        {
            string? relId = slideId.RelationshipId;
            if (string.IsNullOrEmpty(relId)) continue;

            if (presentationPart.GetPartById(relId) is SlidePart slidePart && slidePart.Slide != null)
            {
                var sb = new StringBuilder();
                var textElements = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                foreach (var text in textElements)
                {
                    if (!string.IsNullOrWhiteSpace(text.Text))
                    {
                        sb.AppendLine(text.Text.Trim());
                    }
                }

                string slideText = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(slideText))
                {
                    doc.Pages.Add(new ExtractedPage
                    {
                        PageNumber = slideIndex,
                        Title = $"Slide {slideIndex}",
                        Content = slideText
                    });
                }
            }

            slideIndex++;
        }

        _logger.LogInformation("OpenXmlDocumentTextExtractor.cs - App: Trích xuất PowerPoint thành công, fileName={FileName}, số slide={SlideCount}",
            fileName, doc.Pages.Count);

        return doc;
    }
}

