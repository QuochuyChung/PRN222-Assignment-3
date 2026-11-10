# Tiến độ

Cập nhật file này **trong cùng PR** của task. Phân công chi tiết: [tasks.md](tasks.md).

**Trạng thái:** `Todo` → `In progress` → `Review` → `Merged (Fake)` → `Verified on cluster` (mới được coi là Done).

Cập nhật lần cuối: 2026-11-09

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
| ING-01 | `IFileRepository` (Fake + MinIO) | Todo | | | |
| ING-02 | Trang upload tài liệu | Todo | | | |
| ING-03 | Text extractor PPTX/PDF/DOCX | Todo | | | |
| ING-04 | Chunker | Todo | | | |
| ING-05 | `IEmbeddingRepository` (Fake + Gemini) | Todo | | | |
| ING-06 | `IVectorRepository` (In-memory + Qdrant) | Todo | | | |
| ING-07 | DocumentWorker (`docs-stream`) | Todo | | | |
| ING-08 | Trang danh sách tài liệu, SignalR tiến độ, xóa, re-index | Todo | | | |
| ING-09 | Seed và test ingestion | Todo | | | |

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
| ENG-01 | `ISessionStateRepository` (In-memory + Redis) | Todo | | | |
| ENG-02 | Dịch vụ phiên thi | Todo | | | |
| ENG-03 | State machine | Todo | | | |
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
