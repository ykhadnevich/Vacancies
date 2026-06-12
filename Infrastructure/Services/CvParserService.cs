using Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace Infrastructure.Services;

public class CvParserService : ICvParserService
{
    public Task<string> ExtractTextAsync(Stream pdfStream, CancellationToken ct = default)
    {
        using var pdf = PdfDocument.Open(pdfStream);
        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        return Task.FromResult(text);
    }
}
