using System;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Options;
using AiInterview.Infrastructure.Chunking;
using AiInterview.Infrastructure.Embedding;
using AiInterview.Infrastructure.Extractors;
using AiInterview.Infrastructure.Queue;
using AiInterview.Infrastructure.Repositories;
using AiInterview.Infrastructure.Services;
using AiInterview.Infrastructure.Storage;
using AiInterview.Infrastructure.Vector;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Qdrant.Client;
using StackExchange.Redis;

namespace AiInterview.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiInterviewInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind Options
        services.Configure<AiInterviewOptions>(configuration.GetSection(AiInterviewOptions.SectionName));
        var appOptions = configuration.GetSection(AiInterviewOptions.SectionName).Get<AiInterviewOptions>() ?? new AiInterviewOptions();

        // Common text extraction and chunking
        services.AddSingleton<OpenXmlDocumentTextExtractor>();
        services.AddSingleton<PdfPigDocumentTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, CompositeDocumentTextExtractor>();
        services.AddSingleton<IChunker, SemanticChunker>();

        // Domain Orchestrator
        services.AddScoped<IDocumentService, DocumentService>();

        bool isReal = string.Equals(appOptions.InfraMode, "Real", StringComparison.OrdinalIgnoreCase);

        if (isReal)
        {
            // Real MinIO
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<AiInterviewOptions>>().Value.Minio;
                var config = new AmazonS3Config
                {
                    ServiceURL = opt.Endpoint.StartsWith("http") ? opt.Endpoint : $"http://{opt.Endpoint}",
                    ForcePathStyle = opt.ForcePathStyle,
                    UseHttp = !opt.UseSsl
                };
                return new AmazonS3Client(opt.AccessKey, opt.SecretKey, config);
            });
            services.AddScoped<IFileStorage, MinioFileStorage>();

            // Real MongoDB
            services.AddSingleton<IMongoClient>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<AiInterviewOptions>>().Value.Mongo;
                return new MongoClient(opt.ConnectionString);
            });
            services.AddScoped<IMongoDatabase>(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                var opt = sp.GetRequiredService<IOptions<AiInterviewOptions>>().Value.Mongo;
                return client.GetDatabase(opt.DatabaseName);
            });
            services.AddScoped<IDocumentRepository, MongoDocumentRepository>();

            // Real Redis Stream
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<AiInterviewOptions>>().Value.Redis;
                return ConnectionMultiplexer.Connect(opt.ConnectionString);
            });
            services.AddSingleton<RedisDocumentStreamQueue>();
            services.AddSingleton<IDocumentStreamPublisher>(sp => sp.GetRequiredService<RedisDocumentStreamQueue>());
            services.AddSingleton<IDocumentStreamConsumer>(sp => sp.GetRequiredService<RedisDocumentStreamQueue>());

            // Real Qdrant
            services.AddSingleton(sp =>
            {
                var opt = sp.GetRequiredService<IOptions<AiInterviewOptions>>().Value.Qdrant;
                var uri = new Uri(opt.Endpoint);
                return new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, https: uri.Scheme == "https", apiKey: opt.ApiKey);
            });
            services.AddScoped<IVectorStore, QdrantVectorStore>();

            // Real Gemini Embedding
            services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>();
        }
        else
        {
            // Fake In-Memory mode for local dev
            services.AddSingleton<IFileStorage, FakeFileStorage>();
            services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
            services.AddSingleton<IVectorStore, InMemoryVectorStore>();
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();

            services.AddSingleton<InMemoryDocumentStreamQueue>();
            services.AddSingleton<IDocumentStreamPublisher>(sp => sp.GetRequiredService<InMemoryDocumentStreamQueue>());
            services.AddSingleton<IDocumentStreamConsumer>(sp => sp.GetRequiredService<InMemoryDocumentStreamQueue>());
        }

        return services;
    }
}
