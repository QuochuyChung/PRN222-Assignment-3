using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;

namespace AiInterview.Tests.Helpers;

public static class SampleFileGenerator
{
    public static byte[] CreateSampleDocx(string title, string content)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Add Title
            var titlePara = body.AppendChild(new Paragraph());
            var titleRun = titlePara.AppendChild(new Run());
            titleRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(title));

            // Add Paragraphs
            var para = body.AppendChild(new Paragraph());
            var run = para.AppendChild(new Run());
            run.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text(content));

            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    public static byte[] CreateSamplePptx(string slide1Title, string slide1Content, string slide2Title, string slide2Content)
    {
        using var ms = new MemoryStream();
        using (var presentationDoc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation, true))
        {
            var presPart = presentationDoc.AddPresentationPart();
            presPart.Presentation = new Presentation();
            var slideIdList = presPart.Presentation.AppendChild(new SlideIdList());

            uint slideId = 256;

            void AddSlide(string title, string text)
            {
                var slidePart = presPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(
                    new CommonSlideData(
                        new ShapeTree(
                            new NonVisualGroupShapeProperties(
                                new NonVisualDrawingProperties { Id = 1, Name = "" },
                                new NonVisualGroupShapeDrawingProperties(),
                                new ApplicationNonVisualDrawingProperties()),
                            new GroupShapeProperties(),
                            new Shape(
                                new NonVisualShapeProperties(
                                    new NonVisualDrawingProperties { Id = 2, Name = "TextBox" },
                                    new NonVisualShapeDrawingProperties(new A.ShapeLocks { NoGrouping = true }),
                                    new ApplicationNonVisualDrawingProperties()),
                                new ShapeProperties(),
                                new TextBody(
                                    new A.BodyProperties(),
                                    new A.ListStyle(),
                                    new A.Paragraph(
                                        new A.Run(new A.Text(title))),
                                    new A.Paragraph(
                                        new A.Run(new A.Text(text)))
                                )
                            )
                        )
                    )
                );
                slidePart.Slide.Save();

                var relId = presPart.GetIdOfPart(slidePart);
                slideIdList.Append(new SlideId { Id = slideId++, RelationshipId = relId });
            }

            AddSlide(slide1Title, slide1Content);
            AddSlide(slide2Title, slide2Content);

            presPart.Presentation.Save();
        }
        return ms.ToArray();
    }

    public static byte[] CreateSamplePdfWithText(string text)
    {
        // Minimal standard 1-page PDF containing a font and text stream
        string escapedText = text.Replace("(", "\\(").Replace(")", "\\)");
        string streamContent = $"BT /F1 12 Tf 50 700 Td ({escapedText}) Tj ET";
        int streamLen = Encoding.ASCII.GetByteCount(streamContent);

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        sb.Append("1 0 obj <</Type /Catalog /Pages 2 0 R>> endobj\n");
        sb.Append("2 0 obj <</Type /Pages /Kids [3 0 R] /Count 1>> endobj\n");
        sb.Append("3 0 obj <</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources <</Font <</F1 5 0 R>>>>>> endobj\n");
        sb.Append($"4 0 obj <</Length {streamLen}>> stream\n{streamContent}\nendstream\nendobj\n");
        sb.Append("5 0 obj <</Type /Font /Subtype /Type1 /BaseFont /Helvetica>> endobj\n");
        sb.Append("xref\n0 6\n0000000000 65535 f \n0000000009 00000 n \n0000000056 00000 n \n0000000111 00000 n \n0000000234 00000 n \n0000000318 00000 n \n");
        sb.Append("trailer <</Size 6 /Root 1 0 R>>\nstartxref\n386\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    public static byte[] CreateScannedPdfWithoutText()
    {
        // Minimal standard 1-page PDF containing an empty page (representing an image/scan with no text layer)
        string streamContent = "% No text stream, scanned image only";
        int streamLen = Encoding.ASCII.GetByteCount(streamContent);

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        sb.Append("1 0 obj <</Type /Catalog /Pages 2 0 R>> endobj\n");
        sb.Append("2 0 obj <</Type /Pages /Kids [3 0 R] /Count 1>> endobj\n");
        sb.Append("3 0 obj <</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources <<>>>> endobj\n");
        sb.Append($"4 0 obj <</Length {streamLen}>> stream\n{streamContent}\nendstream\nendobj\n");
        sb.Append("xref\n0 5\n0000000000 65535 f \n0000000009 00000 n \n0000000056 00000 n \n0000000111 00000 n \n0000000208 00000 n \n");
        sb.Append("trailer <</Size 5 /Root 1 0 R>>\nstartxref\n290\n%%EOF\n");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
