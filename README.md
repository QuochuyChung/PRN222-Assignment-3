# PRN222 Assignment 3 — AI Interview

AI đóng vai giám khảo ảo: đọc câu hỏi bằng giọng nói (TTS), sinh viên trả lời bằng giọng nói (STT), AI hỏi xoáy khi câu trả lời mơ hồ, có giới hạn thời gian và số lượt. Câu hỏi và đáp án được sinh từ tài liệu môn học bằng RAG tự code.

Phân công, quy ước và định dạng dữ liệu: xem [tasks.md](tasks.md). Tiến độ: [progress.md](progress.md).

## Kiến trúc 3 lớp

```
Pages/*.cshtml.cs (PageModel)  →  Service  →  Repository  →  SQL / Mongo / Qdrant / Redis / MinIO / Gemini
```

| Project | Vai trò |
|---|---|
| `src/AiInterview.Web` | Razor Pages + code-behind (PageModel), SignalR hub, `Program.cs`. **Chỉ gọi Service** |
| `src/AiInterview.BLL` | Logic nghiệp vụ (prompt, state machine, luật cứng, chunking...). **Chỉ gọi interface Repository** |
| `src/AiInterview.DAL` | Entity, `DbContext`, Repository (interface + Fake + Real), migration, seed |
| `src/AiInterview.Common` | Options, hằng số (tên stream, key Redis), helper log |
| `tests/AiInterview.Tests` | xUnit: unit test + smoke test chạy cả app ở chế độ Fake |

Phụ thuộc một chiều: `Web → BLL → DAL → Common`.

**Ranh giới được khóa bằng máy:**

- Web **không nhìn thấy** DAL: `AiInterview.BLL.csproj` tham chiếu DAL với `PrivateAssets="compile"`, nên `using AiInterview.DAL...` trong Web là lỗi biên dịch. Web chỉ ráp ứng dụng qua `AddServices(...)` và `InitializeApplicationAsync(...)`.
- Service trả **DTO** (`QuestionDto`, `SessionDto`, `TurnDto`, `DocumentDto`...), không trả entity của DAL. Cài đặt service là `internal`, chỉ interface và DTO là `public`.
- Enum dùng chung (`QuestionStatus`, `BloomLevel`, `SessionStatus`, `InterviewState`, `DocumentStatus`) nằm ở `AiInterview.Common.Enums`.
- `tests/AiInterview.Tests/Architecture/ArchitectureTests.cs` kiểm tra các luật trên; vi phạm thì `dotnet test` đỏ.

## Chạy thử (chế độ Fake)

Chế độ Fake dùng dữ liệu giả trong bộ nhớ (SQLite in-memory + repository in-memory), **không cần** Redis, Mongo, Qdrant, MinIO hay Gemini.

```bash
dotnet run --project src/AiInterview.Web
```

```bash
dotnet test
```

Yêu cầu: .NET SDK 8 (`global.json` ghim SDK 8, tự chọn bản 8.0.x mới nhất đang cài).

## Cấu hình

| Khóa | Giá trị | Ý nghĩa |
|---|---|---|
| `Infra:Mode` | `Fake` (mặc định) / `Real` | Chọn bản cài đặt repository |
| `ROLE` (biến môi trường) hoặc `Infra:Role` | `web` / `worker` / `all` (mặc định) | Phần nào của app được bật. Biến `ROLE` ghi đè `Infra:Role` |
| `Database:AutoMigrate` | `false` (mặc định) | Tự chạy migration khi khởi động ở chế độ Real |

Cấu hình đọc từ `appsettings.json` và biến môi trường (dùng `__` thay cho `:`, ví dụ `Infra__Mode=Real`, `Minio__Endpoint=...`). Secret không nằm trong repo.

Endpoint kiểm tra sức khỏe: `/health/live` (tiến trình sống) và `/health/ready` (kết nối được SQL).

## Thêm một chức năng mới

Ví dụ luồng có sẵn: `Pages/Index.cshtml.cs` → `IDashboardService` → `IQuestionRepository` / `ISessionRepository`...

1. **Repository** (`AiInterview.DAL/Repositories/<Khu vực>/`): interface + bản Fake + bản Real.
2. **Đăng ký** repository trong `AiInterview.DAL/Registration/<Module>RepositoryRegistration.cs`, chọn Fake/Real theo `infra.IsFake`.
3. **Service** (`AiInterview.BLL/<Khu vực>/`): interface + cài đặt, chỉ inject interface repository. Đăng ký trong `AiInterview.BLL/Registration/<Module>ServiceRegistration.cs`.
4. **PageModel** (`AiInterview.Web/Pages/...`): inject interface Service qua constructor.

```csharp
public class IndexModel(IDashboardService dashboard) : PageModel
{
    public DashboardSummary Summary { get; private set; } = default!;

    public async Task OnGetAsync(CancellationToken ct) => Summary = await dashboard.GetSummaryAsync(ct);
}
```

Lưu ý: repository SQL và service là `Scoped`. Worker (`BackgroundService`) là singleton nên lấy service qua `IServiceScopeFactory.CreateScope()`.

## Quy ước log

```
<Tên file> - <Service>: <Mô tả chi tiết>
```

```csharp
_logger.Info("MinioFileRepository.cs", ServiceNames.MinIO, "Upload thành công, bucket={Bucket}, key={Key}", bucket, key);
_logger.Error(ex, "QdrantVectorRepository.cs", ServiceNames.Qdrant, "Upsert thất bại, documentId={DocId}", docId);
```

`Service` là một trong `SqlServer`, `MongoDB`, `Qdrant`, `Redis`, `MinIO`, `Gemini`, `SignalR`, `App`. Luôn kèm định danh (`documentId`, `sessionId`, `turnId`...). Không log secret; transcript chỉ log qua `LogText.Preview`.

## Migration (SQL Server)

Entity và `AppDbContext` thuộc phạm vi của M — cần đổi thì báo M.

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add TenMigration --project src/AiInterview.DAL --output-dir Migrations
```

## Ai sở hữu thư mục nào (để tránh đụng nhau khi merge)

| Người | Thư mục |
|---|---|
| **M** | `Common/`, `DAL/AppDbContext.cs`, `DAL/Entities/`, `DAL/Migrations/`, `DAL/Registration/SharedClientsRegistration.cs`, `DAL/Registration/SharedRepositoryRegistration.cs`, `Web/Program.cs`, `Web/Pages/Shared/` |
| **Dev A** | `BLL/Documents/`, `DAL/Repositories/{Documents,Files,Vectors}/`, `DAL/Registration/IngestionRepositoryRegistration.cs`, `BLL/Registration/IngestionServiceRegistration.cs`, `Web/Pages/Documents/`, `Web/Hubs/` |
| **Dev B** | `BLL/{Retrieval,Questions,TurnLog}/`, `DAL/Repositories/{Questions,Turns}/`, các file `Question*` / `TurnLog*` trong `Registration/`, `Web/Pages/{Questions,Logs}/` |
| **Dev C** | `BLL/Interview/`, `DAL/Repositories/Sessions/`, các file `Engine*` trong `Registration/`, phần handler/API của `Web/Pages/Interview/Room.cshtml.cs` |
| **Dev D** | `Web/Pages/{Index,Sessions,Interview}/` (phần giao diện), `Web/wwwroot/` |

Khi hai người cùng cần một file ngoài vùng của mình, báo M trước.
