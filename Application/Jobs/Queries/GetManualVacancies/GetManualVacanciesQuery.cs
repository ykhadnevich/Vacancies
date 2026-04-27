using Application.DTOs;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Jobs.Queries.GetManualVacancies;

public record GetManualVacanciesQuery : IRequest<IReadOnlyList<JobVacancyDto>>;

public class GetManualVacanciesHandler
    : IRequestHandler<GetManualVacanciesQuery, IReadOnlyList<JobVacancyDto>>
{
    private readonly IJobVacancyRepository _jobRepo;

    public GetManualVacanciesHandler(IJobVacancyRepository jobRepo)
    {
        _jobRepo = jobRepo;
    }

    public async Task<IReadOnlyList<JobVacancyDto>> Handle(
        GetManualVacanciesQuery request,
        CancellationToken ct)
    {
        var jobs = await _jobRepo.GetBySourceAsync(JobSource.Manual, ct);

        return jobs.Select(job => new JobVacancyDto
        {
            Id = job.Id,
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            Description = job.Description,
            Salary = job.Salary?.ToString(),
            PrimaryUrl = job.PrimaryUrl,
            AllUrls = job.Urls,
            Source = job.Source,
            WorkFormat = job.WorkFormat,
            SeniorityLevel = job.SeniorityLevel,
            Category = job.Category,
            RelevanceScore = job.RelevanceScore?.Value,
            RelevanceStage = job.RelevanceScore?.Stage.ToString(),
            IsDuplicate = job.IsDuplicate,
            IsManuallyAdded = job.IsManuallyAdded,
            PublishedAt = job.PublishedAt
        }).ToList();
    }
}