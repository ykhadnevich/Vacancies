using MediatR;
using Application.DTOs;

namespace Application.Jobs.Queries.GetJobById;

public record GetJobByIdQuery(Guid Id) : IRequest<JobVacancyDto?>;