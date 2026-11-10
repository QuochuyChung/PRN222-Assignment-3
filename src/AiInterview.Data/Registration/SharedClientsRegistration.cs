using Amazon.Runtime;
using Amazon.S3;
using AiInterview.Common.Configuration;
using AiInterview.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Qdrant.Client;
using StackExchange.Redis;

namespace AiInterview.Data.Registration;

/// <summary>
/// Client dùng chung cho chế độ Real, đăng ký lazy (chỉ kết nối khi có repository lần đầu yêu cầu).
/// Repository Real của từng module chỉ việc inject các client này.
/// </summary>
internal static class SharedClientsRegistration
{
    public static IServiceCollection AddSharedClients(this IServiceCollection services)
    {
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

        services.AddSingleton<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>()
                .GetDatabase(sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(sp.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString));

        services.AddSingleton(sp =>
        {
            var o = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            return new QdrantClient(o.Host, o.Port, o.UseHttps, o.ApiKey);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
            var config = new AmazonS3Config
            {
                ServiceURL = o.Endpoint,
                ForcePathStyle = true // MinIO không dùng virtual-host style
            };
            return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
        });

        services.AddHttpClient(HttpClientNames.Gemini, (sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl);
            if (!string.IsNullOrWhiteSpace(o.ApiKey))
            {
                client.DefaultRequestHeaders.Add("x-goog-api-key", o.ApiKey);
            }
        });

        services.AddHttpClient(HttpClientNames.Ollama, (sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(o.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, o.TimeoutSeconds));
        });

        return services;
    }
}
