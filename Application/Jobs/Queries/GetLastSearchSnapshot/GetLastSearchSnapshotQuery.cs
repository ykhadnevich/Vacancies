using Application.DTOs;
using Application.Jobs.Queries.GetAggregatedJobsV6;
using MediatR;

namespace Application.Jobs.Queries.GetLastSearchSnapshot;

/// <summary>
/// Returns the last v6 result the system computed for the given (user, query) pair.
/// Carries the same search parameters as <see cref="GetAggregatedJobsV6Query"/> so
/// <c>V6QueryHasher</c> computes the same hash on both write and read paths.
/// </summary>
public sealed record GetLastSearchSnapshotQuery(GetAggregatedJobsV6Query SearchParams)
    : IRequest<LastSearchSnapshotResult?>;

public sealed record LastSearchSnapshotResult(
    GetAggregatedJobsV6Result Response,
    DateTime ExecutedAt,
    string QueryHash);
