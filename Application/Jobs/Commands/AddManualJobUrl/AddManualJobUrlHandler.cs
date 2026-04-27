using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using MediatR;

namespace Application.Jobs.Commands.AddManualJobUrl;

public class AddManualJobUrlHandler : IRequestHandler<AddManualJobUrlCommand, AddManualJobUrlResult>
{
    private readonly IEnumerable<IJobSourceService> _sources;
    private readonly IJobVacancyRepository _jobRepo;
    private readonly ISavedUrlRepository _savedUrlRepo;

    public AddManualJobUrlHandler(
        IEnumerable<IJobSourceService> sources,
        IJobVacancyRepository jobRepo,
        ISavedUrlRepository savedUrlRepo)
    {
        _sources = sources;
        _jobRepo = jobRepo;
        _savedUrlRepo = savedUrlRepo;
    }

    public async Task<AddManualJobUrlResult> Handle(
        AddManualJobUrlCommand command, CancellationToken ct)
    {
        var manualSource = _sources.FirstOrDefault(s => s.SourceName == "manual");
        if (manualSource is null)
            return new AddManualJobUrlResult { Success = false, ErrorMessage = "Manual scraper not available" };

        try
        {
            var jobs = await manualSource.FetchJobsAsync(command.Url, ct: ct);

            if (!jobs.Any())
                return new AddManualJobUrlResult { Success = false, ErrorMessage = "No jobs found at this URL" };

            await _jobRepo.AddRangeAsync(jobs, ct);

            var existing = await _savedUrlRepo.GetByUrlAsync(command.Url, ct);

            if (existing is null)
            {
                var savedUrl = SavedUrl.Create(command.Url, command.Alias);
                savedUrl.RecordParsed(jobs.Count);
                await _savedUrlRepo.AddAsync(savedUrl, ct);

                return new AddManualJobUrlResult
                {
                    Success    = true,
                    SavedUrlId = savedUrl.Id,
                    JobsFound  = jobs.Count,
                };
            }
            else
            {
                existing.RecordParsed(jobs.Count);
                await _savedUrlRepo.UpdateAsync(existing, ct);

                return new AddManualJobUrlResult
                {
                    Success    = true,
                    SavedUrlId = existing.Id,
                    JobsFound  = jobs.Count,
                };
            }
        }
        catch (Exception ex)
        {
            return new AddManualJobUrlResult { Success = false, ErrorMessage = $"Failed to parse URL: {ex.Message}" };
        }
    }
}
