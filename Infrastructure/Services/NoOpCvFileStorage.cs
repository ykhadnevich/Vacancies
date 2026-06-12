using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;


public sealed class NoOpCvFileStorage : ICvFileStorage
{
    private readonly ILogger<NoOpCvFileStorage> _logger;

    public NoOpCvFileStorage(ILogger<NoOpCvFileStorage> logger)
    {
        _logger = logger;
        _logger.LogWarning(
            "ICvFileStorage is bound to NoOpCvFileStorage — CV PDFs are " +
            "NOT persisted. Set S3:CvBucket in appsettings to enable real " +
            "S3 storage.");
    }

    public bool IsPersistent => false;

    public Task<string> UploadAsync(
        Stream pdfStream,
        string originalFileName,
        Guid userId,
        CancellationToken ct = default)
    {


        var key = $"local://users/{userId:N}/cv/{DateTime.UtcNow:O}-{originalFileName}";
        return Task.FromResult(key);
    }

    public Task DeleteAsync(string fileKey, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<string> GetPresignedDownloadUrlAsync(
        string fileKey,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
        => throw new NotSupportedException(
            "NoOpCvFileStorage cannot generate presigned URLs. " +
            "Configure S3:CvBucket to enable real S3 storage.");
}
