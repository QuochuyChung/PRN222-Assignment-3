using AiInterview.Common.Logging;
using Microsoft.Extensions.Logging;

namespace AiInterview.Tests.Common;

public class LogExtensionsTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));
    }

    [Fact]
    public void Info_Follows_File_Service_Message_Convention()
    {
        var logger = new CapturingLogger();

        logger.Info("MinioFileRepository.cs", ServiceNames.MinIO, "Upload thành công, bucket={Bucket}, key={Key}", "docs", "a/b.pdf");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("MinioFileRepository.cs - MinIO: Upload thành công, bucket=docs, key=a/b.pdf", entry.Message);
    }

    [Fact]
    public void Error_With_Exception_Keeps_The_Exception()
    {
        var logger = new CapturingLogger();
        var ex = new InvalidOperationException("lỗi gốc");

        logger.Error(ex, "QdrantVectorRepository.cs", ServiceNames.Qdrant, "Upsert thất bại, documentId={DocId}", "doc1");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(ex, entry.Exception);
        Assert.StartsWith("QdrantVectorRepository.cs - Qdrant: Upsert thất bại, documentId=doc1", entry.Message);
    }

    [Fact]
    public void Preview_Truncates_And_Reports_Length()
    {
        var text = new string('a', 200);

        var preview = LogText.Preview(text, 10);

        Assert.StartsWith("aaaaaaaaaa…", preview);
        Assert.Contains("200 ký tự", preview);
    }

    [Fact]
    public void Preview_Handles_Empty()
    {
        Assert.Equal("(rỗng)", LogText.Preview(null));
    }
}
