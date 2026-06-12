using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class BatchNormalizer
{
    private readonly ICvParserService _parser;
    private readonly ICvExtractionService _extractor;
    private readonly SelfConsistencyMerger _merger;
    private readonly ILogger<BatchNormalizer> _logger;

    public BatchNormalizer(
        ICvParserService parser,
        ICvExtractionService extractor,
        SelfConsistencyMerger merger,
        ILogger<BatchNormalizer> logger)
    {
        _parser = parser;
        _extractor = extractor;
        _merger = merger;
        _logger = logger;
    }


    public async Task<List<NormalizationOutput>> NormalizeAllAsync(
        string pdfFolder,
        string outputFolder,
        CancellationToken ct = default,
        int samples = 1)
    {
        if (!Directory.Exists(pdfFolder))
            throw new DirectoryNotFoundException($"PDF folder not found: {pdfFolder}");
        Directory.CreateDirectory(outputFolder);

        var results = new List<NormalizationOutput>();
        var pdfs = Directory.EnumerateFiles(pdfFolder, "*.pdf").OrderBy(f => f).ToList();

        _logger.LogInformation("Found {Count} PDFs in {Folder}", pdfs.Count, pdfFolder);

        foreach (var pdfPath in pdfs)
        {
            if (ct.IsCancellationRequested) break;

            var caseId = Path.GetFileNameWithoutExtension(pdfPath);
            _logger.LogInformation("[Normalize] {CaseId}", caseId);

            try
            {
                using var stream = File.OpenRead(pdfPath);
                var cvText = await _parser.ExtractTextAsync(stream, ct);

                if (string.IsNullOrWhiteSpace(cvText))
                {
                    _logger.LogWarning("[Normalize] {CaseId}: extracted empty text — skipping", caseId);
                    continue;
                }


                var sampleJsons = new List<string>(samples);
                int totalIn = 0, totalOut = 0;
                string modelVersion = string.Empty;
                for (int i = 0; i < samples; i++)
                {
                    if (ct.IsCancellationRequested) break;
                    var r = await _extractor.ExtractAsync(cvText, ct);
                    if (string.IsNullOrWhiteSpace(r.Summary)) continue;
                    sampleJsons.Add(r.Summary);
                    totalIn  += r.InputTokens;
                    totalOut += r.OutputTokens;
                    modelVersion = r.ModelVersion;
                }

                if (sampleJsons.Count == 0)
                {
                    _logger.LogWarning("[Normalize] {CaseId}: all {N} samples returned empty — skipping",
                        caseId, samples);
                    continue;
                }

                var mergedJson = sampleJsons.Count == 1
                    ? sampleJsons[0]
                    : _merger.Merge(sampleJsons);

                var outputPath = Path.Combine(outputFolder, caseId + ".json");
                await File.WriteAllTextAsync(outputPath, mergedJson, ct);

                results.Add(new NormalizationOutput(
                    caseId, mergedJson, modelVersion, totalIn, totalOut));
                _logger.LogInformation(
                    "[Normalize] {CaseId}: done ({Samples}× samples, {Chars} chars, " +
                    "model={Version}, tokens={InTokens}/{OutTokens})",
                    caseId, sampleJsons.Count, mergedJson.Length, modelVersion,
                    totalIn, totalOut);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Normalize] {CaseId}: failed", caseId);
            }
        }

        _logger.LogInformation("Normalization complete: {Success}/{Total} succeeded",
            results.Count, pdfs.Count);
        return results;
    }
}
