using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiInterview.Core.Interfaces;
using AiInterview.Core.Options;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiInterview.Infrastructure.Storage;

public class MinioFileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioFileStorage> _logger;
    private bool _bucketInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public MinioFileStorage(
        IAmazonS3 s3Client,
        IOptions<AiInterviewOptions> options,
        ILogger<MinioFileStorage> logger)
    {
        _s3Client = s3Client;
        _options = options.Value.Minio;
        _logger = logger;
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        if (_bucketInitialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_bucketInitialized) return;

            bool exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _options.BucketName);
            if (!exists)
            {
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = _options.BucketName,
                    UseClientRegion = true
                };
                await _s3Client.PutBucketAsync(putBucketRequest, ct);
                _logger.LogInformation("MinioFileStorage.cs - MinIO: Created bucket, bucket={Bucket}", _options.BucketName);
            }
            _bucketInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinioFileStorage.cs - MinIO: Failed to check/create bucket={Bucket}", _options.BucketName);
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        long size = content.CanSeek ? content.Length : 0;
        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(putRequest, ct);
            _logger.LogInformation("MinioFileStorage.cs - MinIO: Upload succeeded, bucket={Bucket}, key={Key}, size={Size}B",
                _options.BucketName, key, size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinioFileStorage.cs - MinIO: Upload failed, bucket={Bucket}, key={Key}",
                _options.BucketName, key);
            throw;
        }
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(getRequest, ct);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinioFileStorage.cs - MinIO: File read failed, bucket={Bucket}, key={Key}",
                _options.BucketName, key);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, ct);
            _logger.LogInformation("MinioFileStorage.cs - MinIO: File deleted, bucket={Bucket}, key={Key}",
                _options.BucketName, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinioFileStorage.cs - MinIO: File delete failed, bucket={Bucket}, key={Key}",
                _options.BucketName, key);
            throw;
        }
    }
}

