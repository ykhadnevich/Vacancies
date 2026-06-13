using Domain.Enums;
using MediatR;

namespace Application.Jobs.Commands.PrewarmVacancies;

public sealed record PrewarmVacanciesCommand(
    string Keywords,
    string? Location,
    Country Country,
    int MaxNewVacancies = 100
) : IRequest<PrewarmVacanciesResult>;

public sealed record PrewarmVacanciesResult(
    int Scraped,
    int DuplicatesRemoved,
    int NewlyInserted,
    int Normalized,
    int NormalizationFailed,
    int SkillExpansionFailed,
    long DurationMs);
