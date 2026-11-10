using Microsoft.Extensions.Logging;

namespace AiInterview.Common.Logging;

// Message template được ghép động theo đúng quy ước log của nhóm.
#pragma warning disable CA2254

/// <summary>
/// Quy ước log: <c>&lt;Tên file&gt; - &lt;Service&gt;: &lt;Mô tả chi tiết&gt;</c>.
/// <code>
/// _logger.Info("MinioFileRepository.cs", ServiceNames.MinIO, "Upload thành công, bucket={Bucket}, key={Key}", bucket, key);
/// _logger.Error(ex, "QdrantVectorRepository.cs", ServiceNames.Qdrant, "Upsert thất bại, documentId={DocId}", docId);
/// </code>
/// Luôn kèm định danh (documentId, sessionId, turnId...). Không log secret; transcript chỉ log qua <see cref="LogText.Preview"/>.
/// </summary>
public static class LogExtensions
{
    private const string Prefix = "{LogFile} - {LogService}: ";

    public static void Info(this ILogger logger, string file, string service, string message, params object?[] args)
        => logger.LogInformation(Prefix + message, Prepend(file, service, args));

    public static void Warn(this ILogger logger, string file, string service, string message, params object?[] args)
        => logger.LogWarning(Prefix + message, Prepend(file, service, args));

    public static void Error(this ILogger logger, string file, string service, string message, params object?[] args)
        => logger.LogError(Prefix + message, Prepend(file, service, args));

    public static void Error(this ILogger logger, Exception exception, string file, string service, string message, params object?[] args)
        => logger.LogError(exception, Prefix + message, Prepend(file, service, args));

    private static object?[] Prepend(string file, string service, object?[] args)
    {
        var result = new object?[args.Length + 2];
        result[0] = file;
        result[1] = service;
        Array.Copy(args, 0, result, 2, args.Length);
        return result;
    }
}
