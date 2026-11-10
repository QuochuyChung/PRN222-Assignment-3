# Một image duy nhất cho cả hai vai trò (ROLE=web và ROLE=worker).
# Build từ thư mục gốc repo:  docker build -t <harbor>/ai-interview/app:<tag> .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy riêng các file csproj trước để cache bước restore khi chỉ đổi mã nguồn.
COPY global.json AiInterview.sln ./
COPY src/AiInterview.Common/AiInterview.Common.csproj src/AiInterview.Common/
COPY src/AiInterview.Data/AiInterview.Data.csproj src/AiInterview.Data/
COPY src/AiInterview.Services/AiInterview.Services.csproj src/AiInterview.Services/
COPY src/AiInterview.Web/AiInterview.Web.csproj src/AiInterview.Web/
COPY tests/AiInterview.Tests/AiInterview.Tests.csproj tests/AiInterview.Tests/
RUN dotnet restore AiInterview.sln

COPY . .
RUN dotnet publish src/AiInterview.Web/AiInterview.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .

# Cổng 8080 (không cần quyền root). Chế độ Real và các biến khác do Kubernetes truyền vào qua Secret/ConfigMap.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Chạy bằng user không phải root (UID 1654 là user "app" có sẵn trong image .NET 8).
USER 1654
ENTRYPOINT ["dotnet", "AiInterview.Web.dll"]
