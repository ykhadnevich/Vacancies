namespace Application.Common.Interfaces;

public interface ICvParserService
{
    Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken ct = default);
}