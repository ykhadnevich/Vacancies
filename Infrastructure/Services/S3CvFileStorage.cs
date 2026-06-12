using Amazon.S3;
using Amazon.S3.Model;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;


public sealed class S3CvFileStorage : ICvFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<S3CvFileStorage> _logger;

    public S3CvFileStorage(
        IAmazonS3 s3,
        IOptions<S3StorageOptions> options,
        ILogger<S3CvFileStorage> logger)
    {
        _s3 = s3;
        _bucket = options.Value.CvBucket
            ?? throw new InvalidOperationException(
                "S3:CvBucket is not configured. Set it in SSM Parameter Store " +
                "at /vacancies/prod/S3/CvBucket or in appsettings.");
        _logger = logger;
    }

    public bool IsPersistent => true;

    public async Task<string> UploadAsync(
        Stream pdfStream,
        string originalFileName,
        Guid userId,
        CancellationToken ct = default)
    {
        var safeName = SanitizeFileName(originalFileName);
        var key = $"users/{userId:N}/cv/{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{safeName}";

        var put = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = pdfStream,
            ContentType = "application/pdf",
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            AutoCloseStream = false,
            DisablePayloadSigning = false,
        };


        put.Metadata.Add("x-amz-meta-user-id", userId.ToString("N"));

        await _s3.PutObjectAsync(put, ct);
        _logger.LogInformation(
            "Uploaded CV PDF for user {UserId} as s3://{Bucket}/{Key}",
            userId, _bucket, key);
        return key;
    }

    public async Task DeleteAsync(string fileKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileKey)) return;
        try
        {
            await _s3.DeleteObjectAsync(_bucket, fileKey, ct);
            _logger.LogInformation(
                "Deleted CV PDF s3://{Bucket}/{Key}", _bucket, fileKey);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode ==
            System.Net.HttpStatusCode.NotFound)
        {

        }
    }

    public Task<string> GetPresignedDownloadUrlAsync(
        string fileKey,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = fileKey,
            Expires = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(5)),
            Verb = HttpVerb.GET,
        };
        return _s3.GetPreSignedURLAsync(request);
    }

    private static string SanitizeFileName(string name)
    {

        var span = name.AsSpan();
        Span<char> buf = stackalloc char[span.Length];
        var w = 0;
        foreach (var c in span)
        {
            if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')
                buf[w++] = c;
            else
                buf[w++] = '_';
        }
        var safe = new string(buf[..w]);
        if (string.IsNullOrWhiteSpace(safe)) safe = "cv.pdf";
        if (safe.Length > 100) safe = safe[..100];
        return safe;
    }
}


public sealed class S3StorageOptions
{
    public const string SectionName = "S3";
    public string? CvBucket { get; set; }
    public string? Region { get; set; }
}
