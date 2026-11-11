# CLAUDE.md — quy tắc cho agent làm việc trong repo này

Đọc `tasks.md` (phân công, quy ước, định dạng dữ liệu) và `README.md` (kiến trúc, cách chạy) trước khi làm. Chỉ làm đúng task được giao (mã dạng `ING-03`, `QST-02`, `ENG-04`, `UI-05`...), không sang phạm vi của người khác.

## Kiến trúc 3 lớp (bắt buộc)

`PageModel (*.cshtml.cs)` → `Service` → `Repository`. Phụ thuộc một chiều: `Web → BLL → DAL → Common`.

- PageModel **chỉ** gọi Service (inject interface qua constructor), không gọi repository, không chứa logic nghiệp vụ.
- Service chỉ phụ thuộc **interface** repository, giống nhau ở Fake và Real.
- Mọi lời gọi hạ tầng ngoài (SQL, Mongo, Qdrant, Redis, MinIO, **Gemini**) nằm trong Repository.
- Prompt, parse JSON của LLM, state machine, luật cứng nằm ở Service. Repository chỉ trả JSON thô / dữ liệu.

### Ranh giới được khóa bằng máy (đừng cố lách)

- **`AiInterview.Web` không nhìn thấy `AiInterview.DAL`** (`AiInterview.BLL.csproj` tham chiếu DAL với `PrivateAssets="compile"`). Viết `using AiInterview.DAL...` trong Web sẽ lỗi biên dịch. Đừng đổi tham chiếu project để "cho chạy".
- **Service trả DTO** (`QuestionDto`, `SessionDto`, `TurnDto`, `DocumentDto`...), **không bao giờ trả entity/model của DAL** (`Question`, `InterviewTurn`, `DocumentRecord`...). Map bằng `BLL/Mapping/EntityMappers.cs`.
- **Enum dùng chung** (`QuestionStatus`, `BloomLevel`, `SessionStatus`, `InterviewState`, `DocumentStatus`) nằm ở `AiInterview.Common.Enums`, mọi lớp dùng được.
- **Cài đặt service là `internal sealed`**, chỉ interface và DTO là `public`. Test truy cập được nhờ `InternalsVisibleTo`.
- Web ráp ứng dụng chỉ qua `AddServices(...)` và `InitializeApplicationAsync(...)`; lớp DAL được đăng ký bên trong BLL.
- `tests/AiInterview.Tests/Architecture/ArchitectureTests.cs` kiểm tra các luật trên. Test đỏ nghĩa là bạn đã phá ranh giới — sửa code, **không sửa test**.

## Fake / Real

- `Infra:Mode=Fake` là mặc định ở máy dev: không cần Redis/Mongo/Qdrant/MinIO/Gemini thật. Phải chạy được `dotnet run --project src/AiInterview.Web` và `dotnet test` ở chế độ này.
- Task của bạn chỉ xong khi có **cả bản Fake và bản Real** của repository trong phạm vi task, đăng ký trong `DAL/Registration/*RepositoryRegistration.cs`.
- Bản Real không chạy được trên máy dev (hạ tầng ở cluster của M). Nó phải biên dịch được, có log rõ ràng; M sẽ kiểm chứng và ghi lỗi vào `progress.md`.
- Service là `Scoped`. Worker (`BackgroundService`) là singleton: lấy service qua `IServiceScopeFactory`.

## Quy ước log

`<Tên file> - <Service>: <Mô tả chi tiết>` qua helper trong `AiInterview.Common.Logging`:

```csharp
_logger.Info("MinioFileRepository.cs", ServiceNames.MinIO, "Upload thành công, bucket={Bucket}, key={Key}", bucket, key);
_logger.Error(ex, "QdrantVectorRepository.cs", ServiceNames.Qdrant, "Upsert thất bại, documentId={DocId}", docId);
```

Luôn kèm định danh (`documentId`, `sessionId`, `turnId`...). Lỗi phải kèm exception gốc. Không log secret, API key, mật khẩu; transcript chỉ log qua `LogText.Preview`.

## Dữ liệu và hợp đồng

- Không tự đổi interface, entity, `AppDbContext`, định dạng ở `tasks.md` Phụ lục B/C. Cần đổi thì báo M.
- Không hard-code tên model Gemini, endpoint, key. Đọc từ options (`GeminiOptions`, `MinioOptions`...). Secret không bao giờ nằm trong repo.
- Ghi dữ liệu phải **idempotent** (`TurnId` là khóa chính; point id Qdrant = `{documentId}:{chunkIndex}`).
- Luật cứng (hết giờ, hết lượt hỏi xoáy) do server quyết định, không tin hoàn toàn kết quả LLM.

## Hoàn thành một task

- [ ] Fake chạy được, có seed (`IDataSeeder` cho SQL, hoặc `seed/<module>/` cho file mẫu).
- [ ] Real viết xong, biên dịch được, có log theo quy ước.
- [ ] Có unit test cho logic chính; `dotnet build` và `dotnet test` đều xanh.
- [ ] Cập nhật `progress.md` trong cùng PR.
- [ ] Nhánh `feat/<mã-task>-<tên-ngắn>`, PR vào `main`.
