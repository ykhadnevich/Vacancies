namespace Application.Common.Interfaces;


public interface ICvFileStorage
{


    Task<string> UploadAsync(
        Stream pdfStream,
        string originalFileName,
        Guid userId,
        CancellationToken ct = default);


    Task DeleteAsync(string fileKey, CancellationToken ct = default);


    Task<string> GetPresignedDownloadUrlAsync(
        string fileKey,
        TimeSpan? expiry = null,
        CancellationToken ct = default);


    bool IsPersistent { get; }
}
