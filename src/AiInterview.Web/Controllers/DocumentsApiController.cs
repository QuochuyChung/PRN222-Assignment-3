using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiInterview.Web.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsApiController : ControllerBase
{
    private readonly IDocumentRepository _repo;

    public DocumentsApiController(IDocumentRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var docs = await _repo.ListAsync(ct);
        return Ok(docs);
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(string id, CancellationToken ct)
    {
        var doc = await _repo.GetByIdAsync(id, ct);
        if (doc == null)
        {
            return NotFound(new { message = $"Không tìm thấy tài liệu ID: {id}" });
        }

        return Ok(new
        {
            documentId = doc.Id,
            fileName = doc.FileName,
            status = doc.Status.ToString(),
            errorMessage = doc.ErrorMessage,
            chunkCount = doc.ChunkCount,
            pageCount = doc.PageCount,
            updatedAt = doc.UpdatedAt
        });
    }
}

