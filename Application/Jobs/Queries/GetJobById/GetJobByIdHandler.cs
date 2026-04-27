using MediatR;
using Application.DTOs;
using Domain.Interfaces.Repositories;

namespace Application.Jobs.Queries.GetJobById;

public class GetJobByIdHandler : IRequestHandler<GetJobByIdQuery, JobVacancyDto?>
{
    private readonly IJobVacancyRepository _repo;

    public GetJobByIdHandler(IJobVacancyRepository repo)
    {
        _repo = repo;
    }

    public async Task<JobVacancyDto?> Handle(GetJobByIdQuery request, CancellationToken ct)
    {
        var job = await _repo.GetByIdAsync(request.Id, ct);
        if (job is null) return null;

        return new JobVacancyDto
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
        };
    }
}