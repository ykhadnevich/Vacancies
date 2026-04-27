using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using MediatR;

namespace Application.Jobs.Commands.RefreshSavedUrl;

public class RefreshSavedUrlHandler : IRequestHandler<RefreshSavedUrlCommand, RefreshSavedUrlResult>
{
    private readonly ISavedUrlRepository _savedUrlRepo;
    private readonly IJobVacancyRepository _jobRepo;
    private readonly IEnumerable<IJobSourceService> _sources;

    public RefreshSavedUrlHandler(
        ISavedUrlRepository savedUrlRepo,
        IJobVacancyRepository jobRepo,
        IEnumerable<IJobSourceService> sources)
    {
        _savedUrlRepo = savedUrlRepo;
        _jobRepo = jobRepo;
        _sources = sources;
    }

    public async Task<RefreshSavedUrlResult> Handle(
        RefreshSavedUrlCommand request, CancellationToken ct)
    {
        var savedUrl = await _savedUrlRepo.GetByIdAsync(request.SavedUrlId, ct);
        if (savedUrl is null)
            return new RefreshSavedUrlResult
            {
                Success = false,
                ErrorMessage = "Saved URL not found"
            };

        var manualSource = _sources.FirstOrDefault(s => s.SourceName == "manual");
        if (manualSource is null)
            return new RefreshSavedUrlResult
            {
                Success = false,
                ErrorMessage = "Manual scraper not available"
            };

        try
        {
            var jobs = await manualSource.FetchJobsAsync(savedUrl.Url, ct: ct);

            await _jobRepo.DeleteBySourceUrlAsync(savedUrl.Url, ct);

            var newJobs = jobs.ToList();
            if (newJobs.Any())
                await _jobRepo.AddRangeAsync(newJobs, ct);

            savedUrl.RecordParsed(newJobs.Count);
            await _savedUrlRepo.UpdateAsync(savedUrl, ct);

            return new RefreshSavedUrlResult
            {
                Success = true,
                ParsedCount = newJobs.Count,
                AddedCount = newJobs.Count
            };
        }
        catch (Exception ex)
        {
            return new RefreshSavedUrlResult
            {
                Success = false,
                ErrorMessage = $"Failed to refresh: {ex.Message}"
            };
        }
    }
}
