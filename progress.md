# Tiến độ

Cập nhật file này **trong cùng PR** của task. Phân công chi tiết: [tasks.md](tasks.md).

**Trạng thái:** `Todo` → `In progress` → `Review` → `Merged (Fake)` → `Verified on cluster` (mới được coi là Done).

Cập nhật lần cuối: 2026-11-10

## Vướng mắc

Ghi lỗi/blocker ở đây (M ghi lỗi phát hiện khi chạy Real trên cluster, gắn tên người chủ module).

| Ngày | Task | Người xử lý | Mô tả | Trạng thái |
|---|---|---|---|---|
| 2026-11-10 | QST-03/QST-06 | Dev B | `QuestionFlowTests` đỏ sẵn trên `feat/QST-01-retrieval-question-logging` (3/7 test): test dùng chung một `AppDbContext` cho nhiều lượt gọi nên EF báo "another instance with the same key value is already being tracked". Dev C sửa harness sang scope-per-call (giống `EngineTestKit`), không đổi code production. | Đã xử lý |
| 2026-11-10 | QST-06/LOG-01 | Dev B | Ghi chú "testhost bị Application Control chặn" trong `progress.md` không còn đúng: trên máy Dev C chạy được toàn bộ 251 test, gồm test của QST-01…LOG-02. | Cần xác nhận lại |
| 2026-11-10 | QST-04/QST-05/LOG-02 | Dev B | `/Questions` và `/Logs` đã thành trang thật nên `AppSmokeTests.Placeholder_Pages_Render` bỏ hai route này, thay bằng test render nội dung thật. | Đã xử lý |

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
| QST-01 | `IRetrievalService` | Review | | 2026-11-10 | Embed truy vấn, tìm vector, lọc documentId theo hợp đồng đã chốt |
| QST-02 | `IQuestionGeneratorService` + `ILlmRepository` (Gemini) | Review | | 2026-11-10 | Fake/Real LLM, parse JSON và kiểm tra sourceChunkIds |
| QST-03 | Mở rộng `IQuestionRepository` + `IQuestionService` | Review | | 2026-11-10 | Dùng CRUD SQL sẵn có, service xử lý Draft/Approved/Rejected |
| QST-04 | Trang sinh câu hỏi | Review | | 2026-11-10 | Chọn tài liệu Success, số lượng và mức Bloom |
| QST-05 | Trang duyệt câu hỏi | Review | | 2026-11-10 | Sửa, duyệt, loại bỏ bản nháp |
| QST-06 | Seed và test câu hỏi | Review | | 2026-11-10 | Corpus Fake đã index; test retrieval, parse JSON, duyệt và Gemini; build xanh, testhost bị Application Control chặn |
| LOG-01 | TurnLogWorker (`turns-stream` → SQL) | Review | | 2026-11-10 | Retry/dead-letter, ghi idempotent qua ITurnRepository sẵn có; build xanh, testhost bị Application Control chặn |
| LOG-02 | Trang xem log phiên thi | Review | | 2026-11-10 | Lọc sessionId, xem lượt hỏi, transcript, nguồn, thời gian |

## Dev C — Interview Engine

| ID | Việc | Trạng thái | PR | Cập nhật | Ghi chú |
|---|---|---|---|---|---|
| ENG-01 | `ISessionStateRepository` (In-memory + Redis) | Review | | 2026-11-10 | `InMemorySessionStateRepository` (TTL 2h gia hạn theo hoạt động, clone khi đọc/ghi) + `RedisSessionStateRepository` (Hash `interview:{id}`, `SET NX EX` cho `lock:{sessionId}:{turnId}`); test ở `tests/AiInterview.Tests/Engine/` |
| ENG-02 | Dịch vụ phiên thi | Review | | 2026-11-10 | `InterviewSessionService` (tạo/bắt đầu/state/skip/finish) + `QuestionSelector` ưu tiên câu chưa dùng gần đây. **Thêm mới:** `IQuestionUsageRepository` (In-memory + Redis sorted set `question-usage`) để chống trùng giữa thí sinh liền nhau mà không phụ thuộc LOG-01/SQL. State mới trong `SessionState`: `CurrentQuestionText`, `IsFollowUp`, `TurnStartedAt` (cần cho F5 + `TurnMessage.StartedAt`). Seed `EngineDataSeeder` (6 câu `Approved`) |
| ENG-03 | State machine | Review | | 2026-11-10 | `InterviewStateMachine` (internal static): bảng cạnh `AskQuestion → Listening → Evaluating → FollowUp \| NextQuestion \| Finished`, `Finished` là trạng thái cuối; test kiểm **mọi cặp 6×6** trạng thái |
| ENG-04 | `IFollowUpDecider` | Review | | 2026-11-10 | `FollowUpDecider`: prompt + parse JSON Phụ lục C (bỏ hàng rào ```json```, JSON hỏng → không hỏi xoáy thay vì ném lỗi, `needFollowUp=true` mà thiếu câu hỏi → hạ false); 10 test với 3 kiểu trả lời (đủ ý / thiếu ý / mơ hồ). **Đã đăng ký DI** trong `EngineServiceRegistration` (QST-02 đã có `ILlmRepository`); `FakeLlmRepository` thêm kịch bản hỏi xoáy khớp `seed/engine/answers.md` để demo không cần Gemini; test trên composition root thật ở `EngineRegistrationTests` + `SeedScenarioTests` |
| ENG-05 | Luật cứng phía server | Review | | 2026-11-10 | `HardRules`: hết giờ / hết lượt xoáy luôn thắng kết luận LLM, `RemainingSeconds` tính từ deadline tuyệt đối và không âm, im lặng nhắc 1 lần rồi bỏ qua câu. Thêm field `SilencePrompts` vào `SessionState` (chỉ lưu Redis/in-memory, **không** đổi entity/migration) |
| ENG-06 | Chống trùng theo `turnId` | Review | | 2026-11-10 | `SubmitAnswerAsync` gọi `TryLockAsync(sessionId, turnId)` trước mọi việc khác; request trùng trả lại state hiện tại (không gọi LLM, không publish). TTL khóa 5 phút, khóa theo phiên nên `turnId` giống nhau ở 2 phiên vẫn chạy độc lập |
| ENG-07 | Endpoint phiên thi | Review | | 2026-11-10 | 5 handler **Razor Page** trong `Room.cshtml.cs` (không thêm controller nên `Program.cs` không đổi): `Start`, `State` (GET, trả `remainingSeconds`), `SubmitAnswer`, `Skip`, `Finish`. Lỗi map sang 400/404/409 + `{error}`; POST cần antiforgery token (đã thêm `@Html.AntiForgeryToken()`). Sửa lỗi: phiên `Created` (chưa start) không còn nhận câu trả lời |
| ENG-08 | `ITurnPublisher` | Review | | 2026-11-10 | `TurnPublisher` gọi `EnsureGroupAsync` + `PublishAsync(turns-stream, turn.ToFields())`; test round-trip `ToFields` → `FromFields` và lỗi hàng đợi được log rồi ném lại |
| ENG-09 | Khôi phục khi F5 | Review | | 2026-11-10 | `GetStateAsync` trả đúng câu hiện tại (kể cả câu xoáy do LLM sinh, lưu ở `SessionState.CurrentQuestionText`), `followUpCount`, `RemainingSeconds` từ deadline tuyệt đối. Gọi `state` nhiều lần không gia hạn deadline, không tiêu lượt xoáy, không publish. Sửa lỗi: state `Finished` không còn bị ghi lại vào Redis sau `FinishAsync` |
| ENG-10 | Seed và test engine | Review | | 2026-11-10 | `seed/engine/answers.md`: 5 kịch bản trả lời (đủ ý / thiếu ý / mơ hồ / im lặng / quá giờ) cho demo không cần mic; `EngineDataSeeder` (6 câu `Approved`, idempotent, không trùng câu của BASE); test: `SessionStateRepositoryTests`, `InterviewStateMachineTests` (36 cặp), `HardRulesTests`, `TurnPublisherTests`, `FollowUpDeciderTests` (3 kiểu trả lời), `SeedScenarioTests` (5 kịch bản chạy qua Fake LLM thật), `InterviewSessionTests`, `DuplicateTurnTests`, `HardRuleIntegrationTests`, `RestoreAfterRefreshTests`, `EngineSeedTests` |

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
