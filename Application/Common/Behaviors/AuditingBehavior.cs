using System.Text.Json;
using Application.Common.Auditing;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Common.Behaviors;

public sealed class AuditingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditEntryRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditingBehavior<TRequest, TResponse>> _logger;

    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public AuditingBehavior(
        IAuditEntryRepository repository,
        ICurrentUserService currentUser,
        ILogger<AuditingBehavior<TRequest, TResponse>> logger)
    {
        _repository  = repository;
        _currentUser = currentUser;
        _logger      = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not IAuditableRequest auditable)
            return await next(ct);

        var response = await next(ct);

        try
        {
            var (entityType, entityId) = request is IAuditableEntity targeted
                ? (targeted.AuditEntityType, (Guid?)targeted.AuditEntityId)
                : (null, null);

            var payloadJson = SerializePayload(request);

            var entry = AuditEntry.Create(
                action:      auditable.AuditAction,
                userId:      _currentUser.UserId,
                entityType:  entityType,
                entityId:    entityId,
                payloadJson: payloadJson,
                ipAddress:   _currentUser.IpAddress,
                userAgent:   _currentUser.UserAgent);

            await _repository.AddAsync(entry, ct);
        }
        catch (Exception ex)
        {
            // Audit must never fail the original request.
            _logger.LogWarning(ex,
                "Failed to persist audit entry for {Action} (request type {RequestType})",
                auditable.AuditAction, typeof(TRequest).Name);
        }

        return response;
    }

    private static string? SerializePayload(TRequest request)
    {
        if (request is IAuditablePayload customized)
        {
            var payload = customized.BuildAuditPayload();
            return payload is null or { Count: 0 }
                ? null
                : JsonSerializer.Serialize(payload, PayloadJsonOptions);
        }

        try
        {
            return JsonSerializer.Serialize<object>(request, PayloadJsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
