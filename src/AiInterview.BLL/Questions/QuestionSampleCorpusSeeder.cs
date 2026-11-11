using AiInterview.Common.Enums;
using AiInterview.DAL.Models;
using AiInterview.DAL.Repositories.Ai;
using AiInterview.DAL.Repositories.Documents;
using AiInterview.DAL.Repositories.Vectors;
using Microsoft.Extensions.Hosting;

namespace AiInterview.BLL.Questions;

/// <summary>Small indexed corpus for Fake mode so question generation can be tried immediately.</summary>
internal sealed class QuestionSampleCorpusSeeder(IDocumentRepository documents, IEmbeddingRepository embeddings, IVectorRepository vectors) : IHostedService
{
    private const string Id = "sample-razor-pages";

    public async Task StartAsync(CancellationToken ct)
    {
        if (await documents.GetAsync(Id, ct) is not null) return;
        const string content = "Razor Pages tổ chức ứng dụng theo từng trang. Mỗi trang có PageModel xử lý request và file cshtml hiển thị. Routing theo thư mục, phù hợp ứng dụng nhỏ có nhiều trang độc lập. Dependency Injection giúp PageModel gọi service và service gọi repository qua interface.";
        var vector = (await embeddings.EmbedAsync([content], ct))[0];
        await vectors.UpsertAsync([new VectorPoint($"{Id}:0", vector, Id, 0, 1, content)], ct);
        await documents.CreateAsync(new DocumentRecord
        {
            Id = Id, FileName = "Razor Pages (sample)", ContentType = "text/plain", Status = DocumentStatus.Success,
            ChunkCount = 1, PageCount = 1, UploadedBy = "Sample"
        }, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
