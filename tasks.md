# TASKS — AI Interview (Nhóm chức năng 3: Lõi phỏng vấn AI + RAG)

> File này là **nguồn sự thật về phân công**. Tiến độ cập nhật ở `progress.md` (xem mục 2.4).
> Interface và định dạng dữ liệu ở **Phụ lục** là đề xuất ban đầu, **M sẽ chốt chính thức trong task BASE-03**. Muốn đổi thì báo M.

---

## 1. Tổng quan

**Đề tài:** AI đóng vai giám khảo ảo. Đọc câu hỏi bằng giọng nói (TTS), sinh viên trả lời bằng giọng nói (STT), AI hỏi xoáy khi câu trả lời mơ hồ/thiếu ý, có giới hạn thời gian và số lượt hỏi xoáy. Câu hỏi và đáp án được sinh từ tài liệu môn học bằng RAG tự code.

**Không nằm trong phạm vi:** chấm điểm AI (nhóm 4), ghi hình (nhóm 5), báo cáo thống kê (nhóm 6), quản trị tài khoản (nhóm 7). Chưa làm đăng nhập; vai trò giảng viên/sinh viên chọn giả lập trên giao diện.

### 1.1 Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Web | ASP.NET Core Razor Pages, .NET 8, Bootstrap 5.3.3 + Bootstrap Icons (theo template) |
| Dữ liệu chính | SQL Server (dùng lại `mssql` trong cluster), EF Core |
| Metadata tài liệu | MongoDB |
| Vector | Qdrant |
| Trạng thái phiên thi + hàng đợi | Redis (Hash + Streams) |
| File gốc tài liệu | MinIO (S3, bản open source) |
| LLM / embedding | Gemini (project API key). Tên model để trong cấu hình, không hard-code |
| Realtime | SignalR |
| Triển khai | Kubernetes, Helm (OCI trên Harbor), Argo CD, Jenkins, SonarQube, Vault + External Secrets |

### 1.2 Kiến trúc chạy

Một source, một image, một chart. Chart có hai Deployment dùng chung image, khác nhau biến `ROLE` (đặt trong `values.yaml`):

```
1 source → 1 image → 1 chart
                       ├── Deployment web    (ROLE=web)    → Razor Pages + SignalR
                       └── Deployment worker (ROLE=worker) → docs-stream, turns-stream
```

### 1.3 Luồng chính

**Chuẩn bị:** Upload tài liệu → lưu MinIO → Mongo `Processing` → `docs-stream` → worker: trích text → chunk → embedding → Qdrant → Mongo `Success` → sinh câu hỏi bằng LLM → giảng viên duyệt/sửa → ngân hàng câu hỏi (SQL).

**Phỏng vấn:** AI đọc câu hỏi (TTS) → sinh viên nói (STT) → transcript → truy xuất đoạn tài liệu → LLM so với ý chính → `{needFollowUp, followUpQuestion, reason}` → hỏi xoáy hoặc qua câu mới → mỗi lượt xong `XADD turns-stream` → worker ghi `InterviewTurn` vào SQL.

Luật cứng (hết giờ, hết số lượt xoáy) do **server quyết định**, LLM chỉ quyết định đủ ý hay chưa.

---

## 2. Quy ước làm việc

### 2.1 Fake / Real

Mỗi interface có hai bản cài đặt, chọn bằng cấu hình:

```
Infra:Mode = Fake   → máy dev: in-memory / dữ liệu giả, KHÔNG cần Redis/Mongo/Qdrant/MinIO/Gemini thật
Infra:Mode = Real   → K8s: kết nối hạ tầng thật
```

- Chủ module viết **cả Fake và Real** cho interface của module mình.
- M cung cấp interface và các client dùng chung (Mongo, Redis, Qdrant, S3, `DbContext`, đọc cấu hình từ biến môi trường) đã đăng ký sẵn trong DI.
- Bản Real **chỉ chạy được trên cluster** của M. Nó phải biên dịch được và qua review mới merge. Lỗi chỉ lộ khi M chạy thật, M ghi vào mục "Vướng mắc" trong `progress.md`, gắn đúng người chủ module để sửa.
- **Không cần Docker/docker-compose** trên máy dev.

### 2.2 Seed data và test (bắt buộc)

Mỗi người viết seed/test cho module mình (liệt kê trong từng task). Mục tiêu: chạy ở chế độ Fake được ngay, không phụ thuộc người khác. Seed đặt trong `/seed` hoặc `Data/Seed` của module.

### 2.3 Quy ước log

Mọi log ở bản Real (và nơi gọi ra ngoài) theo cú pháp:

```
<Tên file> - <Service>: <Mô tả chi tiết>
```

`<Service>` là một trong: `SqlServer`, `MongoDB`, `Qdrant`, `Redis`, `MinIO`, `Gemini`, `SignalR`, `App` (logic nội bộ).

Ví dụ:

```csharp
_logger.LogInformation("MinioFileStorage.cs - MinIO: Upload thành công, bucket={Bucket}, key={Key}, size={Size}B", bucket, key, size);
_logger.LogError(ex, "QdrantVectorStore.cs - Qdrant: Upsert thất bại, collection={Collection}, documentId={DocId}, số điểm={Count}", col, docId, n);
_logger.LogWarning("GeminiEmbeddingService.cs - Gemini: Bị giới hạn tốc độ, thử lại lần {Attempt}/{Max} sau {DelayMs}ms", a, max, d);
```

Quy tắc:
- Luôn kèm **định danh** để truy vết: `documentId`, `sessionId`, `turnId`, `questionId`, `key`...
- Lỗi phải kèm **exception gốc** (`LogError(ex, ...)`) và tham số đầu vào liên quan.
- **Tuyệt đối không log** secret, API key, mật khẩu, nội dung transcript đầy đủ (chỉ log độ dài hoặc 80 ký tự đầu).
- Log mức `Information` cho mốc quan trọng, `Warning` cho retry, `Error` cho thất bại.

### 2.4 Nhánh, PR và `progress.md`

- Mỗi task một nhánh: `feat/<mã-task>-<tên-ngắn>`, ví dụ `feat/ING-03-pdf-extractor`.
- PR vào `main`, **M duyệt**, thêm một người review chéo: **A ↔ B**, **C ↔ D**.
- **Cập nhật `progress.md` ngay trong PR của task đó** (trạng thái, PR, ngày, ghi chú). Không cập nhật = chưa được merge.
- `main` chỉ nhận PR đã qua build, test và **Quality Gate của SonarQube**. M triển khai `main` lên cluster.
- Không commit secret. Cấu hình đọc từ biến môi trường / `appsettings`. Secret thật nằm ở Vault.
- Đổi interface chung hoặc định dạng dữ liệu ở Phụ lục phải báo M trước.

### 2.5 Trạng thái task

```
Todo → In progress → Review → Merged (Fake chạy) → Verified on cluster (Real chạy)
```

Một task chỉ được coi là **Done** khi đạt **Verified on cluster** (M xác nhận trên K8s).

### 2.6 Định nghĩa "xong" cho một task

- [ ] Bản Fake chạy được, có seed.
- [ ] Bản Real viết xong, biên dịch được, có log theo quy ước 2.3.
- [ ] Có unit test cho logic chính.
- [ ] Qua Quality Gate SonarQube.
- [ ] `progress.md` đã cập nhật.
- [ ] M xác nhận trên cluster.

---

## 3. Phân công

| Người | Vai trò | Phạm vi chính |
|---|---|---|
| **M** | Lead: base project + hạ tầng | Solution, entity, interface, DI, layout shell, Helm, Jenkins, Argo CD, Vault, Ganesha, MinIO, nginx, duyệt PR, xác nhận trên cluster |
| **Dev A** | Document Ingestion | Upload, trích text PPTX/PDF/DOCX, chunk, embedding, Qdrant, Mongo, `docs-stream` worker |
| **Dev B** | Retrieval + Question + Logging | Truy xuất, sinh và duyệt câu hỏi, ghi log lượt thi vào SQL, trang xem log |
| **Dev C** | Interview Engine | Vòng đời phiên thi, state machine, trạng thái Redis, hỏi xoáy bằng LLM, luật cứng, API |
| **Dev D** | Interview UI + Frontend | Phòng phỏng vấn (mic, TTS/STT, countdown, transcript), Dashboard, các trang theo template |

Điền tên: Dev A = `[tên]`, Dev B = `[tên]`, Dev C = `[tên]`, Dev D = `[tên]`.

---

## 4. Task chi tiết

### 4.1 M — Base project (BASE)

| ID | Việc | Phụ thuộc |
|---|---|---|
| BASE-01 | Tạo solution + cấu trúc project (.NET 8, Razor Pages). Mỗi module một thư mục/namespace | — |
| BASE-02 | Entity + `DbContext` + migration đầu: `Question`, `InterviewSession`, `InterviewTurn`. `TurnId` là khóa duy nhất | BASE-01 |
| BASE-03 | Chốt **interface và model dùng chung** (Phụ lục A, B) | BASE-01 |
| BASE-04 | DI chọn Fake/Real theo `Infra:Mode`; chọn phần chạy theo `ROLE` (`web` / `worker` / `all`) | BASE-03 |
| BASE-05 | Layout shell theo template (sidebar, topbar, biến palette `[data-palette]`), trang rỗng cho từng module | BASE-01 |
| BASE-06 | Cấu hình logging + helper theo quy ước 2.3, đọc cấu hình từ biến môi trường | BASE-01 |
| BASE-07 | Dockerfile, cấu hình SonarQube, `progress.md` mẫu, `.gitignore`, hướng dẫn chạy Fake | BASE-01 |

> **Giai đoạn 0:** BASE-01 đến BASE-04 phải xong trước để Dev A–D bắt đầu.

### 4.2 M — Hạ tầng (INF)

| ID | Việc |
|---|---|
| INF-01 | Helm chart `ai-interview`: `deployment-web`, `deployment-worker`, service, ingress, `ExternalSecret`; `values.yaml` có `web.role`, `worker.role`, `image.tag` |
| INF-02 | Vault: thêm `kv/ai-interview/production` (API key Gemini, chuỗi kết nối SQL/Mongo/Redis/Qdrant, key MinIO); mở policy ESO cho đường dẫn mới |
| INF-03 | Argo CD: tạo app `ai-interview`, thêm RBAC (`get`/`update`/`sync`) cho tài khoản dùng trong pipeline |
| INF-04 | Jenkinsfile: build, test, SonarQube + Quality Gate, build image, push image + chart OCI lên Harbor, cập nhật tag, sync Argo CD |
| INF-05 | Ganesha: tạo export riêng cho MongoDB, Qdrant, Redis; PV/PVC (`nfsvers=4.1`, `Retain`); mỗi dịch vụ `replicas: 1`, `strategy: Recreate`, có liveness/readiness probe |
| INF-06 | Triển khai MongoDB, Qdrant, Redis (bật AOF) trên K8s |
| INF-07 | MinIO: bucket `ai-interview-docs`, access key riêng chỉ trên bucket đó, lưu vào Vault |
| INF-08 | `mssql`: tạo database `AiInterview` + login riêng (không dùng `sa`), lưu Vault |
| INF-09 | nginx + Cloudflare Tunnel + certbot cho domain mới; **thêm header WebSocket** (`Upgrade`, `Connection "upgrade"`) cho SignalR |
| INT-xx | Xác nhận từng task của Dev A–D trên cluster, ghi lỗi vào `progress.md` |

### 4.3 Dev A — Document Ingestion (ING)

| ID | Việc | Phụ thuộc |
|---|---|---|
| ING-01 | `IFileStorage`: `FakeFileStorage` (bộ nhớ/đĩa) + `MinioFileStorage` (AWSSDK.S3, `ForcePathStyle = true`) | BASE-03 |
| ING-02 | Trang upload: kiểm tra loại file (pptx/pdf/docx) và dung lượng, lưu file, ghi Mongo `Processing`, `XADD docs-stream` (**thứ tự: file → Mongo → stream**) | ING-01 |
| ING-03 | `IDocumentTextExtractor`: DOCX/PPTX bằng `DocumentFormat.OpenXml`, PDF bằng `PdfPig`. PDF scan không có text → báo lỗi rõ, đánh dấu `Failed` kèm lý do | BASE-03 |
| ING-04 | `IChunker`: chia đoạn theo slide/trang/heading, kích thước + độ chồng lấn cấu hình được, giữ metadata (số trang/slide, chỉ số chunk) | ING-03 |
| ING-05 | `IEmbeddingService`: `FakeEmbedding` (vector giả ổn định theo nội dung) + `GeminiEmbeddingService`. **Gộp batch, giới hạn tốc độ, retry có backoff** | BASE-03 |
| ING-06 | `IVectorStore`: `InMemoryVectorStore` + `QdrantVectorStore`. **Upsert idempotent**: point ID = `{documentId}:{chunkIndex}` | BASE-03 |
| ING-07 | `DocumentWorker` (ROLE=worker): đọc `docs-stream` theo consumer group, xử lý, cập nhật Mongo, retry, dead-letter; tên consumer theo tên pod | ING-01…06 |
| ING-08 | Trang danh sách tài liệu: trạng thái, SignalR đẩy tiến độ (kèm API lấy trạng thái hiện tại khi F5), xóa (Mongo + Qdrant + MinIO trong một hàm `DeleteDocumentAsync`), re-index | ING-02, ING-07 |
| ING-09 | **Seed/test:** file mẫu PPTX/PDF/DOCX nhỏ có tiếng Việt, một PDF scan để thử lỗi; unit test extractor + chunker | — |

### 4.4 Dev B — Retrieval, Question, Logging (QST / LOG)

| ID | Việc | Phụ thuộc |
|---|---|---|
| QST-01 | `IRetrievalService`: `FakeRetrieval` (chunk mẫu) + bản Real (embed câu truy vấn, search Qdrant, lọc theo `documentId`/môn) | BASE-03 |
| QST-02 | `IQuestionGenerator`: prompt + parse JSON (Phụ lục C), `FakeQuestionGenerator` + `GeminiQuestionGenerator`. Mỗi câu có đáp án, ý chính, mức Bloom, `sourceChunkIds` | BASE-03 |
| QST-03 | Repository câu hỏi (SQL): CRUD, trạng thái `Draft`/`Approved`/`Rejected` | BASE-02 |
| QST-04 | Trang sinh câu hỏi: chọn tài liệu `Success` → chọn số lượng/mức Bloom → sinh → danh sách `Draft` | QST-02, QST-03 |
| QST-05 | Trang duyệt: sửa nội dung/đáp án/ý chính, duyệt hoặc loại bỏ; chỉ câu `Approved` vào ngân hàng chính thức | QST-04 |
| LOG-01 | `TurnLogWorker` (ROLE=worker): đọc `turns-stream`, ghi `InterviewTurn` vào SQL, **idempotent theo `TurnId`**, retry, dead-letter | BASE-02 |
| LOG-02 | Trang xem log một phiên thi: câu hỏi, transcript, lượt xoáy, lý do, nguồn trích dẫn, thời gian | LOG-01 |
| QST-06 | **Seed/test:** bộ chunk mẫu, bộ câu hỏi + đáp án + ý chính + Bloom; unit test parse JSON và luồng duyệt | — |

### 4.5 Dev C — Interview Engine (ENG)

| ID | Việc | Phụ thuộc |
|---|---|---|
| ENG-01 | `ISessionStateStore`: `InMemorySessionStateStore` + `RedisSessionStateStore` (Hash `interview:{sessionId}`, TTL 2 giờ gia hạn theo hoạt động; lưu `deadline` tuyệt đối) | BASE-03 |
| ENG-02 | Dịch vụ phiên thi: tạo phiên (chọn tập câu hỏi `Approved`, **tránh trùng giữa các thí sinh thi liền nhau**), cấu hình số câu chính, số lượt xoáy tối đa, thời gian mỗi câu | BASE-02, ENG-01 |
| ENG-03 | State machine: `AskQuestion → Listening → Evaluating → FollowUp \| NextQuestion \| Finish`, có kiểm thử cho mọi chuyển trạng thái | ENG-01 |
| ENG-04 | `IFollowUpDecider`: prompt + JSON (Phụ lục C), `FakeFollowUpDecider` (theo kịch bản mẫu) + `GeminiFollowUpDecider`; kèm đoạn tài liệu từ `IRetrievalService` | BASE-03 |
| ENG-05 | **Luật cứng phía server**: hết giờ, hết lượt xoáy → chuyển câu; kiểm tra lại thời gian dù client gửi gì; im lặng → nhắc một lần rồi bỏ qua | ENG-03 |
| ENG-06 | Chống trùng: khóa `SET NX` theo `turnId`; request trùng không gọi LLM lần hai | ENG-01 |
| ENG-07 | Endpoint (page handler/API): `start`, `state` (trả `remainingSeconds`), `submit-answer`, `skip`, `finish` | ENG-03…06 |
| ENG-08 | `ITurnPublisher`: `XADD turns-stream` khi một lượt hoàn tất (Phụ lục B) | BASE-03 |
| ENG-09 | Khôi phục khi F5: `state` trả đúng câu hiện tại, số lượt xoáy đã dùng, thời gian còn lại | ENG-07 |
| ENG-10 | **Seed/test:** câu trả lời mẫu ba kiểu (đủ ý, thiếu ý, mơ hồ) cho từng câu; unit test state machine + luật cứng + chống trùng | — |

### 4.6 Dev D — Interview UI + Frontend (UI)

| ID | Việc | Phụ thuộc |
|---|---|---|
| UI-01 | Dựng các trang khung theo template cho từng module (sidebar menu, topbar, card), dùng layout từ BASE-05 | BASE-05 |
| UI-02 | Trang chọn phiên thi / bắt đầu thi, kiểm tra quyền mic (nút "Test mic") | UI-01 |
| UI-03 | Giao diện phòng phỏng vấn: thẻ câu hỏi của AI, nút mic lớn, vòng đếm ngược, khung transcript, chỉ báo trạng thái (đang hỏi / đang nghe / đang xử lý) | UI-01 |
| UI-04 | TTS bằng `speechSynthesis` (`vi-VN`), sự kiện đọc xong để mở mic | UI-03 |
| UI-05 | STT bằng Web Speech API: kết quả tạm thời hiển thị live, phát hiện im lặng, **ô nhập chữ dự phòng** khi mic/STT lỗi | UI-03 |
| UI-06 | Client state machine nối với endpoint ENG-07: gửi transcript kèm `turnId`, đếm ngược từ `remainingSeconds` của server, xử lý F5 | UI-04, UI-05, ENG-07 |
| UI-07 | Màn hình kết thúc + xử lý lỗi (mất mạng, API lỗi, thử lại câu hiện tại) | UI-06 |
| UI-08 | Dashboard: card hero, KPI (số tài liệu, câu hỏi chờ duyệt, số phiên thi), bảng lượt hỏi/đáp gần đây, dữ liệu từ API/Fake | UI-01 |
| UI-09 | **Seed/test:** transcript mẫu để dựng UI không cần mic thật; dữ liệu giả cho Dashboard | — |

---

## 5. Thứ tự và phụ thuộc

```
Giai đoạn 0 (M)        BASE-01 → 04 (interface, DI Fake/Real, ROLE)
        │
Giai đoạn 1 (song song) Dev A: ING   Dev B: QST/LOG   Dev C: ENG   Dev D: UI
        │                (mỗi người chạy bằng Fake; M dựng hạ tầng INF song song)
        │
Giai đoạn 2 (tích hợp)  A → B: chunk thật cho sinh câu hỏi
                         B → C: câu hỏi Approved thật cho phiên thi
                         C ↔ D: Engine ↔ UI voice
        │
Giai đoạn 3             Triển khai K8s đầy đủ, sửa lỗi, kịch bản demo, kiểm thử
```

**Mốc thời gian:** điền sau khi biết deadline.

| Mốc | Nội dung | Hạn |
|---|---|---|
| M0 | Base + interface xong, mọi người bắt đầu | `[ngày]` |
| M1 | Tất cả module chạy được bằng Fake | `[ngày]` |
| M2 | Real chạy trên cluster, tích hợp các module | `[ngày]` |
| M3 | Kịch bản demo + sửa lỗi + tài liệu | `[ngày]` |

---

## 6. Review chéo

| Người | Review chéo bởi |
|---|---|
| Dev A | Dev B |
| Dev B | Dev A |
| Dev C | Dev D |
| Dev D | Dev C |
| Tất cả | M duyệt cuối |

---

## Phụ lục A — Interface đề xuất (M chốt ở BASE-03)

```csharp
public interface IFileStorage {
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
public interface IDocumentTextExtractor {
    Task<ExtractedDocument> ExtractAsync(Stream content, string fileName, CancellationToken ct);
}
public interface IChunker {
    IReadOnlyList<ChunkDraft> Chunk(ExtractedDocument doc);
}
public interface IEmbeddingService {
    Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct);
}
public interface IVectorStore {
    Task UpsertAsync(IReadOnlyList<VectorPoint> points, CancellationToken ct);
    Task<IReadOnlyList<VectorHit>> SearchAsync(float[] vector, int topK, VectorFilter? filter, CancellationToken ct);
    Task DeleteByDocumentAsync(string documentId, CancellationToken ct);
}
public interface IDocumentRepository { /* MongoDB: Create, Get, List, UpdateStatus, Delete */ }
public interface IRetrievalService {
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(string query, RetrievalScope scope, int topK, CancellationToken ct);
}
public interface IQuestionGenerator {
    Task<IReadOnlyList<QuestionDraft>> GenerateAsync(GenerateRequest request, CancellationToken ct);
}
public interface IFollowUpDecider {
    Task<FollowUpDecision> DecideAsync(FollowUpRequest request, CancellationToken ct);
}
public interface ISessionStateStore {
    Task<SessionState?> GetAsync(string sessionId, CancellationToken ct);
    Task SaveAsync(string sessionId, SessionState state, CancellationToken ct);
    Task<bool> TryLockAsync(string sessionId, string turnId, TimeSpan ttl, CancellationToken ct);
    Task DeleteAsync(string sessionId, CancellationToken ct);
}
public interface ITurnPublisher {
    Task PublishAsync(TurnMessage turn, CancellationToken ct);
}
```

## Phụ lục B — Định dạng dữ liệu

**Trạng thái (enum)**

| Đối tượng | Giá trị |
|---|---|
| Document | `Uploaded`, `Processing`, `Success`, `Failed` (kèm `errorMessage`) |
| Question | `Draft`, `Approved`, `Rejected` |
| Session | `Created`, `InProgress`, `Completed`, `Abandoned` |
| Mức Bloom | `Remember`, `Understand`, `Apply`, `Analyze` |

**Mongo — `documents`:** `_id` (documentId), `fileName`, `contentType`, `size`, `storageKey` (MinIO), `status`, `errorMessage`, `chunkCount`, `pageCount`, `uploadedBy`, `uploadedAt`, `updatedAt`.

**Qdrant — collection `chunks`:** point ID = `{documentId}:{chunkIndex}`; payload: `documentId`, `chunkIndex`, `page` (trang/slide), `text`. Số chiều vector lấy từ cấu hình `Embedding:Dimensions`.

**MinIO:** bucket `ai-interview-docs`, key `{documentId}/{fileName}`.

**Redis**

| Key | Kiểu | Nội dung |
|---|---|---|
| `interview:{sessionId}` | Hash | `state`, `questionIndex`, `questionId`, `followUpCount`, `deadline` (UTC, tuyệt đối), TTL 2 giờ (gia hạn khi có hoạt động) |
| `lock:{sessionId}:{turnId}` | String (`SET NX EX`) | khóa chống trùng |
| `docs-stream` | Stream | `documentId`, `storageKey` |
| `turns-stream` | Stream | `turnId`, `sessionId`, `questionId`, `question`, `transcript`, `isFollowUp`, `needFollowUp`, `reason`, `sources`, `startedAt`, `endedAt` |

Group consumer: `docs-workers` cho `docs-stream`, `sql-writers` cho `turns-stream`. Tên consumer lấy từ tên pod.

**SQL:** `Question(Id, Content, ReferenceAnswer, KeyPoints, BloomLevel, SourceDocumentId, SourceChunkIds, Status, ...)`, `InterviewSession(Id, ..., Status, ...)`, `InterviewTurn(TurnId UNIQUE, SessionId, QuestionId, IsFollowUp, QuestionText, Transcript, Reason, Sources, StartedAt, EndedAt)`.

## Phụ lục C — JSON giữa app và LLM

**Sinh câu hỏi (QST-02) — đầu ra là mảng:**
```json
[
  {
    "content": "Razor Pages khác MVC ở điểm nào?",
    "referenceAnswer": "…",
    "keyPoints": ["PageModel", "routing theo thư mục", "phù hợp app nhỏ"],
    "bloomLevel": "Understand",
    "sourceChunkIds": ["doc1:3", "doc1:4"]
  }
]
```

**Quyết định hỏi xoáy (ENG-04):**
```json
{
  "needFollowUp": true,
  "followUpQuestion": "Khi nào bạn chọn Razor Pages thay vì MVC?",
  "reason": "Chưa nêu được tình huống chọn Razor Pages",
  "coveredKeyPoints": ["PageModel"],
  "missingKeyPoints": ["routing theo thư mục", "phù hợp app nhỏ"],
  "sources": ["doc1:3"]
}
```
Server **không tin hoàn toàn** vào `needFollowUp`: nếu `followUpCount` đã đạt tối đa hoặc hết giờ thì luôn chuyển câu.

## Phụ lục D — Việc chưa chốt

- Prompt chi tiết cho sinh câu hỏi và hỏi xoáy (Dev B, Dev C đề xuất, M duyệt).
- Chiến lược chunk cụ thể (kích thước, độ chồng lấn).
- Có cần đăng nhập thật hay không (hiện: giả lập vai trò).
- Kịch bản demo và dữ liệu mẫu cuối.
