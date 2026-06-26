using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace OstQuiz.Api.Services.Storage;

/// <summary>
/// S3-compatible object storage backed by MinIO. Uses AWSSDK.S3 with path-style
/// addressing so the same code works against real AWS S3 later.
/// </summary>
public class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3;
    private readonly StorageOptions _opts;
    private readonly ILogger<S3ObjectStorage> _log;

    public S3ObjectStorage(IAmazonS3 s3, IOptions<StorageOptions> opts, ILogger<S3ObjectStorage> log)
    {
        _s3 = s3;
        _opts = opts.Value;
        _log = log;
    }

    public async Task EnsureBucketsAsync(CancellationToken ct = default)
    {
        foreach (var bucket in new[] { _opts.AudioBucket, _opts.CoverBucket })
        {
            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3, bucket);
            if (!exists)
            {
                await _s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
                _log.LogInformation("Created bucket {Bucket}", bucket);
            }
        }
    }

    public Task PutAudioAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        => PutAsync(_opts.AudioBucket, key, content, contentType, ct);

    public Task PutCoverAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        => PutAsync(_opts.CoverBucket, key, content, contentType, ct);

    private async Task PutAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct)
    {
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);
    }

    public Task<ObjectStream> GetAudioAsync(string key, CancellationToken ct = default)
        => GetAsync(_opts.AudioBucket, key, ct);

    public Task<bool> AudioExistsAsync(string key, CancellationToken ct = default)
        => ExistsAsync(_opts.AudioBucket, key, ct);

    public Task<ObjectStream> GetCoverAsync(string key, CancellationToken ct = default)
        => GetAsync(_opts.CoverBucket, key, ct);

    public Task<bool> CoverExistsAsync(string key, CancellationToken ct = default)
        => ExistsAsync(_opts.CoverBucket, key, ct);

    private async Task<ObjectStream> GetAsync(string bucket, string key, CancellationToken ct)
    {
        var resp = await _s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct);
        return new ObjectStream(
            resp.ResponseStream,
            string.IsNullOrEmpty(resp.Headers.ContentType) ? "application/octet-stream" : resp.Headers.ContentType,
            resp.ContentLength <= 0 ? null : resp.ContentLength);
    }

    private async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            await _s3.GetObjectMetadataAsync(bucket, key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
