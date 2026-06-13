using System.Text.Json;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.DTOs;
using Application.Jobs.Queries.GetAggregatedJobsV6;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Jobs.Queries.GetLastSearchSnapshot;

public sealed class GetLastSearchSnapshotHandler
    : IRequestHandler<GetLastSearchSnapshotQuery, LastSearchSnapshotResult?>
{
    private readonly IUserSearchSnapshotRepository _snapshots;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<GetLastSearchSnapshotHandler> _logger;

    public GetLastSearchSnapshotHandler(
        IUserSearchSnapshotRepository snapshots,
        ICurrentUserService currentUser,
        ILogger<GetLastSearchSnapshotHandler> logger)
    {
        _snapshots = snapshots;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<LastSearchSnapshotResult?> Handle(
        GetLastSearchSnapshotQuery query, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new UnauthorizedAccessException("Authentication required.");

        var queryHash = V6QueryHasher.Compute(query.SearchParams);
        var snapshot = await _snapshots.GetByQueryAsync(userId, queryHash, ct);
        if (snapshot is null) return null;

        if (!snapshot.IsCurrentSchema())
        {
            _logger.LogInformation(
                "UserSearchSnapshot {Id} schema {Old} != current {Current} — treating as cache miss; UI will trigger fresh /v6.",
                snapshot.Id, snapshot.SchemaVersion, UserSearchSnapshot.CurrentSchemaVersion);
            return null;
        }

        try
        {
            var response = JsonSerializer.Deserialize<GetAggregatedJobsV6Result>(snapshot.ResponseJson);
            if (response is null) return null;
            return new LastSearchSnapshotResult(response, snapshot.ExecutedAt, queryHash);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to deserialise UserSearchSnapshot {Id} — treating as cache miss.",
                snapshot.Id);
            return null;
        }
    }
}
