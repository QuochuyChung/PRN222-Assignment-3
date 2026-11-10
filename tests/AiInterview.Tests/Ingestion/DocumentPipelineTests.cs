using AiInterview.Common.Constants;
using AiInterview.Common.Enums;
using AiInterview.Data.Repositories.Ai;
using AiInterview.Services.Documents;

namespace AiInterview.Tests.Ingestion;

public class DocumentPipelineTests
{
    [Fact]
    public async Task Upload_Saves_File_Then_Metadata_Then_Queues_Message()
    {
        var kit = new IngestionTestKit();
        var bytes = "%PDF fake"u8.ToArray();

        var dto = await kit.Service.UploadAsync(new UploadDocumentRequest(@"C:\Users\me\..\Bài giảng 1.pdf", "application/pdf", new MemoryStream(bytes), bytes.Length, "test"));

        Assert.Equal("Bài giảng 1.pdf", dto.FileName);
        Assert.Equal(DocumentStatus.Processing, dto.Status);
        var record = await kit.Documents.GetAsync(dto.Id);
        Assert.Equal($"{dto.Id}/Bài giảng 1.pdf", record!.StorageKey);
        await using var stored = await kit.Files.OpenReadAsync(record.StorageKey);
        Assert.Equal(bytes.Length, stored.Length);
        var message = Assert.Single(kit.Queue.Peek(QueueNames.DocsStream));
        Assert.Equal(dto.Id, message.Fields["documentId"]);
        Assert.Equal(record.StorageKey, message.Fields["storageKey"]);
    }

    [Theory]
    [InlineData("notes.txt", 10)]
    [InlineData("empty.pdf", 0)]
    [InlineData("huge.pdf", 51L * 1024 * 1024)]
    public async Task Upload_Rejects_Bad_Files_Before_Storing_Anything(string name, long size)
    {
        var kit = new IngestionTestKit();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            kit.Service.UploadAsync(new UploadDocumentRequest(name, "x", new MemoryStream(), size, null)));

        Assert.Equal(0, await kit.Documents.CountAsync());
        Assert.Empty(kit.Queue.Peek(QueueNames.DocsStream));
    }

    [Theory]
    [InlineData("sample.docx", 4)]
    [InlineData("sample.pptx", 3)]
    [InlineData("sample.pdf", 2)]
    public async Task Worker_Processes_Seed_Files_To_Success(string seed, int pages)
    {
        var kit = new IngestionTestKit();
        var dto = await kit.UploadSeedAsync(seed);

        await kit.RunWorkerUntilAsync(async () => (await kit.Documents.GetAsync(dto.Id))!.Status != DocumentStatus.Processing);

        var record = (await kit.Documents.GetAsync(dto.Id))!;
        Assert.Equal(DocumentStatus.Success, record.Status);
        Assert.Equal(pages, record.PageCount);
        Assert.True(record.ChunkCount > 0);
        Assert.Equal(record.ChunkCount, kit.Vectors.Count);
        var hits = await kit.Vectors.SearchAsync(new float[64], 100);
        Assert.Contains(hits, h => h.Id == $"{dto.Id}:0");
    }

    [Fact]
    public async Task Scanned_Pdf_Fails_At_Once_Without_Retry_Or_Dead_Letter()
    {
        var kit = new IngestionTestKit();
        var dto = await kit.UploadSeedAsync("scanned_error.pdf");

        await kit.RunWorkerUntilAsync(async () => (await kit.Documents.GetAsync(dto.Id))!.Status != DocumentStatus.Processing);

        var record = (await kit.Documents.GetAsync(dto.Id))!;
        Assert.Equal(DocumentStatus.Failed, record.Status);
        Assert.Contains("scan", record.ErrorMessage);
        Assert.Empty(kit.Queue.Peek(QueueNames.DeadLetter(QueueNames.DocsStream)));
    }

    [Fact]
    public async Task Transient_Errors_Are_Retried_Then_Dead_Lettered()
    {
        var embeddings = new BrokenEmbeddings();
        var kit = new IngestionTestKit(embeddings);
        var dto = await kit.UploadSeedAsync("sample.docx");

        await kit.RunWorkerUntilAsync(async () => (await kit.Documents.GetAsync(dto.Id))!.Status != DocumentStatus.Processing
                                                  && kit.Queue.Peek(QueueNames.DeadLetter(QueueNames.DocsStream)).Count > 0);

        var record = (await kit.Documents.GetAsync(dto.Id))!;
        Assert.Equal(DocumentStatus.Failed, record.Status);
        Assert.Contains("after 3 attempts", record.ErrorMessage);
        Assert.Equal(3, embeddings.Calls);
        var dead = Assert.Single(kit.Queue.Peek(QueueNames.DeadLetter(QueueNames.DocsStream)));
        Assert.Equal(dto.Id, dead.Fields["documentId"]);
        Assert.Contains("Gemini is down", dead.Fields["reason"]);
    }

    [Fact]
    public async Task Delete_Removes_Metadata_Vectors_And_File()
    {
        var kit = new IngestionTestKit();
        var dto = await kit.UploadSeedAsync("sample.pptx");
        await kit.RunWorkerUntilAsync(async () => (await kit.Documents.GetAsync(dto.Id))!.Status == DocumentStatus.Success);
        var key = (await kit.Documents.GetAsync(dto.Id))!.StorageKey;

        await kit.Service.DeleteAsync(dto.Id);

        Assert.Null(await kit.Documents.GetAsync(dto.Id));
        Assert.Equal(0, kit.Vectors.Count);
        await Assert.ThrowsAsync<FileNotFoundException>(() => kit.Files.OpenReadAsync(key));
    }

    [Fact]
    public async Task Reindex_Clears_Vectors_And_Queues_Again_But_Not_While_Processing()
    {
        var kit = new IngestionTestKit();
        var dto = await kit.UploadSeedAsync("sample.pdf");
        await Assert.ThrowsAsync<InvalidOperationException>(() => kit.Service.ReindexAsync(dto.Id));
        await kit.RunWorkerUntilAsync(async () => (await kit.Documents.GetAsync(dto.Id))!.Status == DocumentStatus.Success);

        await kit.Service.ReindexAsync(dto.Id);

        Assert.Equal(DocumentStatus.Processing, (await kit.Documents.GetAsync(dto.Id))!.Status);
        Assert.Equal(0, kit.Vectors.Count);
        Assert.Single(kit.Queue.Peek(QueueNames.DocsStream));
    }

    private sealed class BrokenEmbeddings : IEmbeddingRepository
    {
        public int Calls;

        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Calls);
            throw new HttpRequestException("Gemini is down");
        }
    }
}
