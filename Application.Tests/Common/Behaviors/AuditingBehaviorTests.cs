using Application.Common.Auditing;
using Application.Common.Behaviors;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.Tests.Common.Behaviors;

public class AuditingBehaviorTests
{
    private sealed record PlainRequest(string Value) : IRequest<string>;

    private sealed record AuditableRequest(Guid TargetId, string Comment)
        : IRequest<string>, IAuditableRequest, IAuditableEntity
    {
        public string AuditAction     => "TestAction";
        public string AuditEntityType => "TestEntity";
        public Guid   AuditEntityId   => TargetId;
    }

    private sealed record CustomPayloadRequest(string Secret, string Public)
        : IRequest<string>, IAuditableRequest, IAuditablePayload
    {
        public string AuditAction => "CustomPayload";

        public IReadOnlyDictionary<string, object?>? BuildAuditPayload() => new Dictionary<string, object?>
        {
            ["public"] = Public,
            // Secret is intentionally omitted.
        };
    }

    private sealed class FakeAuditRepo : IAuditEntryRepository
    {
        public List<AuditEntry> Entries { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task AddAsync(AuditEntry entry, CancellationToken ct = default)
        {
            if (ShouldThrow)
                throw new InvalidOperationException("simulated DB failure");
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEntry>> QueryByUserAsync(Guid userId, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditEntry>>(Array.Empty<AuditEntry>());

        public Task<IReadOnlyList<AuditEntry>> QueryByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AuditEntry>>(Array.Empty<AuditEntry>());
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public bool IsAuthenticated => UserId.HasValue;
        public string? IpAddress { get; set; } = "127.0.0.1";
        public string? UserAgent { get; set; } = "xunit/1.0";
    }

    private static AuditingBehavior<TReq, TResp> Make<TReq, TResp>(FakeAuditRepo repo, FakeCurrentUser user)
        where TReq : notnull
        => new(repo, user, NullLogger<AuditingBehavior<TReq, TResp>>.Instance);

    [Fact]
    public async Task NonAuditableRequest_DoesNotWriteEntry()
    {
        var repo = new FakeAuditRepo();
        var behavior = Make<PlainRequest, string>(repo, new FakeCurrentUser());

        var result = await behavior.Handle(
            new PlainRequest("v"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Empty(repo.Entries);
    }

    [Fact]
    public async Task AuditableRequest_WritesEntryWithCorrectFields()
    {
        var repo  = new FakeAuditRepo();
        var user  = new FakeCurrentUser { UserId = Guid.NewGuid(), IpAddress = "1.2.3.4", UserAgent = "ua" };
        var target = Guid.NewGuid();
        var behavior = Make<AuditableRequest, string>(repo, user);

        await behavior.Handle(
            new AuditableRequest(target, "hi"),
            _ => Task.FromResult("ok"),
            CancellationToken.None);

        var entry = Assert.Single(repo.Entries);
        Assert.Equal("TestAction", entry.Action);
        Assert.Equal("TestEntity", entry.EntityType);
        Assert.Equal(target, entry.EntityId);
        Assert.Equal(user.UserId, entry.UserId);
        Assert.Equal("1.2.3.4", entry.IpAddress);
        Assert.Equal("ua", entry.UserAgent);
        Assert.Equal("Success", entry.Outcome);
        Assert.NotNull(entry.PayloadJson);
        Assert.Contains("\"comment\"", entry.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomPayload_OverridesDefaultSerialization()
    {
        var repo = new FakeAuditRepo();
        var behavior = Make<CustomPayloadRequest, string>(repo, new FakeCurrentUser());

        await behavior.Handle(
            new CustomPayloadRequest(Secret: "sensitive", Public: "ok-to-log"),
            _ => Task.FromResult("done"),
            CancellationToken.None);

        var entry = Assert.Single(repo.Entries);
        Assert.NotNull(entry.PayloadJson);
        Assert.Contains("ok-to-log", entry.PayloadJson);
        Assert.DoesNotContain("sensitive", entry.PayloadJson);
    }

    [Fact]
    public async Task RepositoryFailure_IsSwallowed_OriginalResponseReturned()
    {
        var repo = new FakeAuditRepo { ShouldThrow = true };
        var behavior = Make<AuditableRequest, string>(repo, new FakeCurrentUser());

        var result = await behavior.Handle(
            new AuditableRequest(Guid.NewGuid(), "x"),
            _ => Task.FromResult("payload-ok"),
            CancellationToken.None);

        Assert.Equal("payload-ok", result);
        Assert.Empty(repo.Entries);
    }

    [Fact]
    public async Task HandlerException_PreventsAuditWrite()
    {
        var repo = new FakeAuditRepo();
        var behavior = Make<AuditableRequest, string>(repo, new FakeCurrentUser());

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new AuditableRequest(Guid.NewGuid(), "x"),
            _ => throw new InvalidOperationException("handler boom"),
            CancellationToken.None));

        // Behaviour writes ONLY on the success path (post-await). A throwing
        // handler must not produce a "Success" audit row.
        Assert.Empty(repo.Entries);
    }
}
