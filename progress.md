# TIẾN ĐỘ DỰ ÁN — AI Interview (Nhóm 3: Lõi phỏng vấn AI + RAG)

> **Mã task Jira/Board:** `PRN222-22`  
> **Tên task:** Document Ingestion (upload, trích text, chunk, embedding, Qdrant)  
> **Người thực hiện:** Dev A  
> **Nhánh:** `feat/document-ingestion`  
> **Người review chéo:** Dev B  
> **Người duyệt:** Lead M  

---

## 1. Trạng thái tổng quan

```
[x] Todo → [x] In progress → [x] Review → [x] Merged (Fake chạy thành công) → [ ] Verified on cluster (M xác nhận trên K8s)
```

- **Môi trường Fake (Dev machine):** Hoạt động hoàn toàn (In-memory storage, queue Channel, fake deterministic embeddings, in-memory vector store). Không cần Redis, Mongo, Qdrant, MinIO thật.
- **Môi trường Real (Kubernetes cluster):** Đã hoàn thiện toàn bộ mã nguồn theo chuẩn K8s (Minio S3 SDK, MongoDB Driver, Redis Stream consumer group, Qdrant Grpc Client, Google Gemini Batch Embedding API), tuân thủ định dạng log mục 2.3.
- **Unit Tests:** 14/14 unit tests passed (100%).

---

## 2. Bảng theo dõi chi tiết các task con (ING-01 đến ING-09)

| Task ID | Nội dung chi tiết | Bản Fake | Bản Real | Unit Test | Trạng thái | Ghi chú |
|---|---|:---:|:---:|:---:|:---:|---|
| **ING-01** | `IFileStorage`: `FakeFileStorage` (In-memory) + `MinioFileStorage` (AWSSDK.S3, `ForcePathStyle = true`, tự tạo bucket `ai-interview-docs`) | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Log đúng format: `MinioFileStorage.cs - MinIO: Upload thành công...` |
| **ING-02** | Trang upload tài liệu: kiểm tra định dạng (.pptx, .pdf, .docx), dung lượng (<= 50MB), lưu file, ghi Mongo `Processing`, `XADD docs-stream` (thứ tự nghiêm ngặt: file → Mongo → stream) | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Drag & drop UI + validation Razor Page `Documents/Upload` |
| **ING-03** | `IDocumentTextExtractor`: Word DOCX & PowerPoint PPTX (`DocumentFormat.OpenXml`), PDF (`PdfPig`). Phát hiện PDF scan không có text → ném `ScannedPdfException`, đánh dấu `Failed` kèm lý do chi tiết | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Hỗ trợ tiếng Việt UTF-8, bóc tách cấu trúc từng slide PPTX và trang PDF |
| **ING-04** | `IChunker`: `SemanticChunker` phân đoạn theo slide PPTX / trang PDF / heading DOCX. Cho phép cấu hình `ChunkSize` và `ChunkOverlap`, bảo lưu metadata (`page`, `chunkIndex`, `documentId`) | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Cấu hình trong `AiInterview:Chunking` |
| **ING-05** | `IEmbeddingService`: `FakeEmbedding` (vector giả định 768 chiều ổn định theo text băm, chuẩn hóa L2) + `GeminiEmbeddingService` (`text-embedding-004`, gộp batch, rate limit Semaphore, exponential backoff) | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Log đúng format: `GeminiEmbeddingService.cs - Gemini: ...` |
| **ING-06** | `IVectorStore`: `InMemoryVectorStore` (Cosine Similarity) + `QdrantVectorStore` (gRPC QdrantClient). Upsert idempotent với point ID = `{documentId}:{chunkIndex}` (Deterministic UUID) | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Tự động tạo collection `chunks` với vector size từ cấu hình |
| **ING-07** | `DocumentWorker` (BackgroundService với `ROLE=worker` hoặc `ROLE=all`): đọc `docs-stream` theo consumer group `docs-workers`, consumer name theo pod name / hostname, cập nhật Mongo, retry 3 lần, dead-letter | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Hoạt động trơn tru cả In-Memory Channel và Redis Stream |
| **ING-08** | Trang danh sách tài liệu (`/Documents`): hiển thị trạng thái, SignalR `DocumentHub` đẩy tiến độ realtime, API `/api/documents/{id}/status` khi F5, nút Re-index và nút Xóa đồng bộ (`DeleteDocumentAsync` xóa MinIO + Mongo + Qdrant) | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | Tích hợp Bootstrap Icons, hiệu ứng spinner và thanh tiến độ động |
| **ING-09** | Seed data & Unit Test: Thư mục `seed/` chứa 4 file mẫu tiếng Việt (`sample.docx`, `sample.pptx`, `sample.pdf`, `scanned_error.pdf`); Bộ 14 unit test toàn diện cho extractor, chunker, embedding, worker và flow xóa | :white_check_mark: | :white_check_mark: | :white_check_mark: | **Review** | 14/14 tests pass |

---

## 3. Cấu trúc thư mục mã nguồn

```
PRN222-Assignment-3/
├── AiInterview.sln
├── seed/                                      # File mẫu phục vụ seed và kiểm thử
│   ├── sample.docx                            # Giáo trình mẫu Word tiếng Việt
│   ├── sample.pptx                            # Slide bài giảng 2 slide tiếng Việt
│   ├── sample.pdf                             # Tài liệu PDF chuẩn có text layer
│   └── scanned_error.pdf                      # PDF scan giả lập không có text để test bắt lỗi
├── src/
│   ├── AiInterview.Core/                      # Thư viện Core chứa Models, Interfaces & Options
│   │   ├── Exceptions/
│   │   │   └── ScannedPdfException.cs
│   │   ├── Interfaces/
│   │   │   ├── IFileStorage.cs
│   │   │   ├── IDocumentTextExtractor.cs
│   │   │   ├── IChunker.cs
│   │   │   ├── IEmbeddingService.cs
│   │   │   ├── IVectorStore.cs
│   │   │   ├── IDocumentRepository.cs
│   │   │   ├── IDocumentStreamQueue.cs
│   │   │   ├── IDocumentService.cs
│   │   │   └── IDocumentProgressNotifier.cs
│   │   ├── Models/
│   │   │   ├── DocumentStatus.cs
│   │   │   ├── DocumentMetadata.cs
│   │   │   ├── ExtractedDocument.cs
│   │   │   ├── ChunkDraft.cs
│   │   │   ├── VectorModels.cs
│   │   │   └── DocStreamMessage.cs
│   │   └── Options/
│   │       └── AiInterviewOptions.cs
│   │
│   ├── AiInterview.Infrastructure/            # Triển khai Fake và Real cho toàn bộ hạ tầng
│   │   ├── Chunking/
│   │   │   └── SemanticChunker.cs
│   │   ├── DependencyInjection/
│   │   │   └── ServiceCollectionExtensions.cs # Đăng ký DI tự động theo InfraMode
│   │   ├── Embedding/
│   │   │   ├── FakeEmbeddingService.cs
│   │   │   └── GeminiEmbeddingService.cs
│   │   ├── Extractors/
│   │   │   ├── OpenXmlDocumentTextExtractor.cs
│   │   │   ├── PdfPigDocumentTextExtractor.cs
│   │   │   └── CompositeDocumentTextExtractor.cs
│   │   ├── Queue/
│   │   │   ├── InMemoryDocumentStreamQueue.cs
│   │   │   └── RedisDocumentStreamQueue.cs
│   │   ├── Repositories/
│   │   │   ├── InMemoryDocumentRepository.cs
│   │   │   └── MongoDocumentRepository.cs
│   │   ├── Services/
│   │   │   └── DocumentService.cs             # Điều phối upload, ingest, reindex, delete
│   │   ├── Storage/
│   │   │   ├── FakeFileStorage.cs
│   │   │   └── MinioFileStorage.cs
│   │   └── Vector/
│   │       ├── InMemoryVectorStore.cs
│   │       └── QdrantVectorStore.cs
│   │
│   └── AiInterview.Web/                       # Web Razor Pages, SignalR Hub & Background Worker
│       ├── Controllers/
│       │   └── DocumentsApiController.cs      # API tra cứu trạng thái khi F5
│       ├── Hubs/
│       │   └── DocumentHub.cs                 # SignalR Hub phát sự kiện tiến độ
│       ├── Pages/
│       │   ├── Documents/
│       │   │   ├── Index.cshtml               # Danh sách tài liệu realtime
│       │   │   ├── Index.cshtml.cs
│       │   │   ├── Upload.cshtml              # Trang kéo thả file upload
│       │   │   └── Upload.cshtml.cs
│       │   ├── Shared/
│       │   │   └── _Layout.cshtml             # Giao diện Shell Dashboard
│       │   ├── Index.cshtml                   # Dashboard tổng quan
│       │   └── Index.cshtml.cs
│       ├── Services/
│       │   └── SignalRDocumentProgressNotifier.cs
│       ├── Workers/
│       │   └── DocumentWorker.cs              # BackgroundService tiêu thụ docs-stream
│       ├── appsettings.json                   # Cấu hình Fake / Real & tham số model
│       └── Program.cs                         # Khởi chạy phân vai theo biến ROLE
│
└── tests/
    └── AiInterview.Tests/                     # Bộ Unit Tests toàn diện
        ├── DocumentServiceTests.cs
        ├── DocumentWorkerTests.cs
        ├── FakeEmbeddingServiceTests.cs
        ├── GenerateSeedFilesTest.cs
        ├── OpenXmlDocumentTextExtractorTests.cs
        ├── PdfPigDocumentTextExtractorTests.cs
        ├── SemanticChunkerTests.cs
        └── Helpers/
            └── SampleFileGenerator.cs
```

---

## 4. Hướng dẫn chạy thử nghiệm

### 4.1 Chạy chế độ Fake (Mặc định cho máy Dev)
Chế độ `Fake` không cần cài đặt hay chạy Docker Redis, Mongo, MinIO hay Qdrant:

```bash
# Di chuyển vào project web
cd src/AiInterview.Web

# Chạy ứng dụng
dotnet run
```
Truy cập trình duyệt tại: `http://localhost:5000` hoặc port hiển thị trên terminal:
- Truy cập menu **"Quản lý Tài liệu (Dev A)"** (`/Documents`).
- Nhấn **"Tải lên tài liệu mới"** (`/Documents/Upload`).
- Có thể dùng ngay các file trong thư mục `seed/` (`sample.docx`, `sample.pptx`, `sample.pdf`, `scanned_error.pdf`).
- Quan sát SignalR tự động cập nhật tiến độ trích xuất, chunking và embedding sang `Hoàn thành` (hoặc `Thất bại` nếu chọn file scan).

### 4.2 Chạy toàn bộ Unit Tests
```bash
dotnet test AiInterview.sln
```
Kết quả: **14 Passed / 0 Failed**.

### 4.3 Chạy chế độ Real trên K8s
Chuyển biến môi trường hoặc trong `appsettings.json`:
- `AiInterview:InfraMode = "Real"`
- `ROLE = "web"` cho Web pod, `ROLE = "worker"` cho Worker pod (hoặc `ROLE = "all"`).

---

## 5. Vướng mắc & Nhật ký xử lý

| Ngày | Vấn đề | Hướng giải quyết | Người xử lý |
|---|---|---|:---:|
| 10/11/2026 | Qdrant point ID yêu cầu kiểu UUID/UInt64, không nhận chuỗi thô `{documentId}:{chunkIndex}` | Sử dụng cơ chế băm Deterministic MD5 sang Guid cho ID point của Qdrant, đồng thời lưu trữ chuỗi ID gốc trong payload để đảm bảo tính Idempotent 100% | Dev A |
| 10/11/2026 | Nhận diện file PDF scan không có lớp text layer | Kiểm tra tổng số ký tự trích xuất từ tất cả các trang qua `PdfPig`. Nếu nhỏ hơn 10 ký tự, ném ngoại lệ chuyên biệt `ScannedPdfException` và chuyển Mongo status sang `Failed` kèm thông báo tiếng Việt rõ ràng | Dev A |
| 10/11/2026 | Cần đảm bảo xóa đồng bộ tránh rác dữ liệu | Tích hợp trong hàm duy nhất `DeleteDocumentAsync`: xóa file vật lý trên MinIO, xóa toàn bộ vector point tương ứng trên Qdrant, sau đó xóa bản ghi metadata trên MongoDB | Dev A |

