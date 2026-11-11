using AiInterview.Common.Configuration;
using AiInterview.Common.Logging;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.DAL.Repositories.Files;

/// <summary>Original document files in MinIO (S3 API). Key: <c>{documentId}/{fileName}</c>.</summary>
public sealed class MinioFileRepository(
    IAmazonS3 s3,
    IOptions<MinioOptions> options,
    ILogger<MinioFileRepository> logger) : IFileRepository
{
    private const string File = "MinioFileRepository.cs";
    private readonly string _bucket = options.Value.Bucket;
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _bucketReady;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false
            }, ct);
            logger.Info(File, ServiceNames.MinIO, "Upload succeeded, bucket={Bucket}, key={Key}, size={Size}B",
                _bucket, key, content.CanSeek ? content.Length : -1);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.MinIO, "Upload failed, bucket={Bucket}, key={Key}", _bucket, key);
            throw;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            // Copy into memory and dispose the S3 response right away so the HTTP connection goes back to the pool.
            using var response = await s3.GetObjectAsync(_bucket, key, ct);
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            logger.Info(File, ServiceNames.MinIO, "Download succeeded, bucket={Bucket}, key={Key}, size={Size}B", _bucket, key, buffer.Length);
            return buffer;
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.MinIO, "Download failed, bucket={Bucket}, key={Key}", _bucket, key);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await s3.DeleteObjectAsync(_bucket, key, ct);
            logger.Info(File, ServiceNames.MinIO, "Delete succeeded, bucket={Bucket}, key={Key}", _bucket, key);
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.MinIO, "Delete failed, bucket={Bucket}, key={Key}", _bucket, key);
            throw;
        }
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketReady) return;
        await _bucketLock.WaitAsync(ct);
        try
        {
            if (_bucketReady) return;
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3, _bucket))
            {
                await s3.PutBucketAsync(_bucket, ct);
                logger.Info(File, ServiceNames.MinIO, "Created bucket, bucket={Bucket}", _bucket);
            }
            _bucketReady = true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, File, ServiceNames.MinIO, "Could not check or create bucket, bucket={Bucket}", _bucket);
            throw;
        }
        finally
        {
            _bucketLock.Release();
        }
    }
}
