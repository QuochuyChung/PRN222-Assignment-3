# Tiến độ

Cập nhật file này **trong cùng PR** của task. Phân công chi tiết: [tasks.md](tasks.md).

**Trạng thái:** `Todo` → `In progress` → `Review` → `Merged (Fake)` → `Verified on cluster` (mới được coi là Done).

Cập nhật lần cuối: 2026-11-10

## Vướng mắc

Ghi lỗi/blocker ở đây (M ghi lỗi phát hiện khi chạy Real trên cluster, gắn tên người chủ module).

| Ngày | Task | Người xử lý | Mô tả | Trạng thái |
|---|---|---|---|---|
| | | | | |

## M — Base project

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| BASE-01 | Solution + cấu trúc project (3 lớp) | Review | | 2026-11-09 | `Web → Services → Data → Common` + tests |
| BASE-02 | Entity, DbContext, migration đầu | Review | | 2026-11-09 | Migration `InitialCreate` (SQL Server), `TurnId` là khóa chính |
| BASE-03 | Interface và model dùng chung | Review | | 2026-11-09 | Xem Phụ lục A của tasks.md |
| BASE-04 | DI Fake/Real theo `Infra:Mode`, chọn phần chạy theo `ROLE` | Review | | 2026-11-09 | `AddData` / `AddServices` |
| BASE-05 | Layout shell theo template | Review | | 2026-11-09 | Bảng màu `ocean`, `violet`; đổi tông ở `wwwroot/css/site.css` |
| BASE-06 | Logging helper + cấu hình từ env | Review | | 2026-11-09 | `Info` / `Warn` / `Error` theo quy ước |
| BASE-07 | progress.md, .gitignore, README, CLAUDE.md | Review | | 2026-11-09 | Dockerfile + SonarQube chưa làm (thuộc hạ tầng) |

## M — Hạ tầng

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| INF-01 | Helm chart `ai-interview` | Todo | | | |
| INF-02 | Vault KV + policy ESO | Todo | | | |
| INF-03 | Argo CD app + RBAC | Todo | | | |
| INF-04 | Jenkinsfile | Todo | | | |
| INF-05 | Ganesha export + PV/PVC cho Mongo, Qdrant, Redis | Todo | | | |
| INF-06 | Triển khai MongoDB, Qdrant, Redis | Todo | | | |
| INF-07 | MinIO bucket + access key | Todo | | | |
| INF-08 | `mssql` database + login | Todo | | | |
| INF-09 | nginx, tunnel, cert, header WebSocket | Todo | | | |

## Dev A — Document Ingestion

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| ING-01 | `IFileRepository` (Fake + MinIO) | Review | | 2026-11-10 | `FakeFileRepository` + `MinioFileRepository` (tự tạo bucket, đóng response S3 ngay) |
| ING-02 | Trang upload tài liệu | Review | | 2026-11-10 | Kiểm tra loại/dung lượng ở client và service; thứ tự file → Mongo `Processing` → `docs-stream` |
| ING-03 | Text extractor PPTX/PDF/DOCX | Review | | 2026-11-10 | `DocumentTextExtractor`: PDF theo trang, PPTX theo slide, DOCX theo heading; PDF scan/hỏng → `Failed` không retry |
| ING-04 | Chunker | Review | | 2026-11-10 | Cửa sổ trượt theo trang, cắt ở câu/khoảng trắng; `Ingestion:ChunkSize`/`ChunkOverlap` |
| ING-05 | `IEmbeddingRepository` (Fake + Gemini) | Review | | 2026-11-10 | Fake: feature hashing ổn định; Gemini: batch 50, giãn cách 250ms, retry 429/5xx có backoff |
| ING-06 | `IVectorRepository` (In-memory + Qdrant) | Review | | 2026-11-10 | Point id = UUID băm từ `{documentId}:{chunkIndex}` (giữ id gốc trong payload) |
| ING-07 | DocumentWorker (`docs-stream`) | Review | | 2026-11-10 | Retry `Ingestion:MaxAttempts` lần → `Failed` + `docs-stream-dead`; Redis `XAUTOCLAIM` nhận lại message treo; consumer = HOSTNAME |
| ING-08 | Trang danh sách tài liệu, SignalR tiến độ, xóa, re-index | Review | | 2026-11-10 | Hub `/hubs/documents`, polling `?handler=Status` khi mất SignalR; `DeleteAsync` xóa vector + file + metadata |
| ING-09 | Seed và test ingestion | Review | | 2026-11-10 | `seed/ingestion/` (DOCX/PPTX/PDF tiếng Việt + PDF scan), test ở `tests/AiInterview.Tests/Ingestion/` |

## Dev B — Retrieval, Question, Logging

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| QST-01 | `IRetrievalService` | Todo | | | |
| QST-02 | `IQuestionGeneratorService` + `ILlmRepository` (Gemini) | Todo | | | |
| QST-03 | Mở rộng `IQuestionRepository` + `IQuestionService` | Todo | | | Repository SQL cơ bản đã có |
| QST-04 | Trang sinh câu hỏi | Todo | | | |
| QST-05 | Trang duyệt câu hỏi | Todo | | | |
| QST-06 | Seed và test câu hỏi | Todo | | | |
| LOG-01 | TurnLogWorker (`turns-stream` → SQL) | Todo | | | `ITurnRepository.InsertIfNotExistsAsync` đã có |
| LOG-02 | Trang xem log phiên thi | Todo | | | |

## Dev C — Interview Engine

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| ENG-01 | `ISessionStateRepository` (In-memory + Redis) | Review | | 2026-11-10 | `InMemorySessionStateRepository` (TTL 2h gia hạn theo hoạt động, clone khi đọc/ghi) + `RedisSessionStateRepository` (Hash `interview:{id}`, `SET NX EX` cho `lock:{sessionId}:{turnId}`); test ở `tests/AiInterview.Tests/Engine/` |
| ENG-02 | Dịch vụ phiên thi | Todo | | | |
| ENG-03 | State machine | Review | | 2026-11-10 | `InterviewStateMachine` (internal static): bảng cạnh `AskQuestion → Listening → Evaluating → FollowUp \| NextQuestion \| Finished`, `Finished` là trạng thái cuối; test kiểm **mọi cặp 6×6** trạng thái |
| ENG-04 | `IFollowUpDecider` | Todo | | | |
| ENG-05 | Luật cứng phía server | Todo | | | |
| ENG-06 | Chống trùng theo `turnId` | Todo | | | |
| ENG-07 | Endpoint phiên thi | Todo | | | |
| ENG-08 | `ITurnPublisher` | Todo | | | `TurnMessage` đã có |
| ENG-09 | Khôi phục khi F5 | Todo | | | |
| ENG-10 | Seed và test engine | Todo | | | |

## Dev D — Interview UI

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| UI-01 | Trang khung theo template | Todo | | | Đã có trang khung ở `Web/Pages` |
| UI-02 | Trang chọn phiên thi + test mic | Todo | | | |
| UI-03 | Giao diện phòng phỏng vấn | Todo | | | |
| UI-04 | TTS | Todo | | | |
| UI-05 | STT + ô nhập chữ dự phòng | Todo | | | |
| UI-06 | Client state machine nối endpoint | Todo | | | |
| UI-07 | Màn hình kết thúc + xử lý lỗi | Todo | | | |
| UI-08 | Dashboard | Todo | | | Dashboard mẫu đã có (luồng 3 lớp) |
| UI-09 | Seed và test UI | Todo | | | |
