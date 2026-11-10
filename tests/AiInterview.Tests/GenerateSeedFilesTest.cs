using System.IO;
using AiInterview.Tests.Helpers;
using Xunit;

namespace AiInterview.Tests;

public class GenerateSeedFilesTest
{
    [Fact]
    public void GenerateSeedFiles_ShouldCreateAllSampleFilesUnderSeedFolder()
    {
        string baseDir = Directory.GetCurrentDirectory();
        // Traverse up to find repo root
        var dirInfo = new DirectoryInfo(baseDir);
        while (dirInfo != null && !File.Exists(Path.Combine(dirInfo.FullName, "tasks.md")))
        {
            dirInfo = dirInfo.Parent;
        }

        string repoRoot = dirInfo != null ? dirInfo.FullName : baseDir;
        string seedDir = Path.Combine(repoRoot, "seed");
        Directory.CreateDirectory(seedDir);

        // 1. Sample DOCX
        string docxPath = Path.Combine(seedDir, "sample.docx");
        if (!File.Exists(docxPath))
        {
            byte[] docx = SampleFileGenerator.CreateSampleDocx(
                "Giao trinh PRN222 - Lap trinh .NET nang cao",
                "Mon hoc PRN222 bao gom cac kien thuc ve ASP.NET Core Razor Pages, Entity Framework Core, Dependency Injection, kien truc microservices va RAG voi Vector Database Qdrant. Sinh vien can nam ro cach hoat dong cua PageModel, luong request-response va xu ly du lieu da luong.");
            File.WriteAllBytes(docxPath, docx);
        }

        // 2. Sample PPTX
        string pptxPath = Path.Combine(seedDir, "sample.pptx");
        if (!File.Exists(pptxPath))
        {
            byte[] pptx = SampleFileGenerator.CreateSamplePptx(
                "Slide 1: Tong quan AI Interview",
                "He thong phong van ao AI ung dung LLM va RAG de tu dong hoa quy trinh danh gia ky nang sinh vien qua giong noi.",
                "Slide 2: Pipeline Ingestion tai lieu",
                "Tai lieu duoc trich xuat text bang OpenXml va PdfPig, phan doan chunking ngu nghia, sinh embedding bang Gemini va luu tru vao Qdrant.");
            File.WriteAllBytes(pptxPath, pptx);
        }

        // 3. Sample PDF
        string pdfPath = Path.Combine(seedDir, "sample.pdf");
        if (!File.Exists(pdfPath))
        {
            byte[] pdf = SampleFileGenerator.CreateSamplePdfWithText(
                "Huong dan thuc hanh PRN222: Cai dat va van hanh he thong phong van AI voi Kubernetes va Qdrant.");
            File.WriteAllBytes(pdfPath, pdf);
        }

        // 4. Scanned PDF (error test)
        string scannedPdfPath = Path.Combine(seedDir, "scanned_error.pdf");
        if (!File.Exists(scannedPdfPath))
        {
            byte[] scannedPdf = SampleFileGenerator.CreateScannedPdfWithoutText();
            File.WriteAllBytes(scannedPdfPath, scannedPdf);
        }

        Assert.True(File.Exists(Path.Combine(seedDir, "sample.docx")));
        Assert.True(File.Exists(Path.Combine(seedDir, "sample.pptx")));
        Assert.True(File.Exists(Path.Combine(seedDir, "sample.pdf")));
        Assert.True(File.Exists(Path.Combine(seedDir, "scanned_error.pdf")));
    }
}

