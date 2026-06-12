using Domain.Entities;

namespace Domain.Tests.Entities;

public class AuditEntryTests
{
    [Fact]
    public void Create_WithMinimalArgs_FillsRequiredFieldsAndDefaultsOutcomeToSuccess()
    {
        var before = DateTime.UtcNow;
        var entry = AuditEntry.Create(
            action:      "DoThing",
            userId:      null,
            entityType:  null,
            entityId:    null,
            payloadJson: null,
            ipAddress:   null,
            userAgent:   null);
        var after = DateTime.UtcNow;

        Assert.NotEqual(Guid.Empty, entry.Id);
        Assert.Equal("DoThing", entry.Action);
        Assert.Equal("Success", entry.Outcome);
        Assert.Null(entry.UserId);
        Assert.Null(entry.EntityType);
        Assert.Null(entry.EntityId);
        Assert.Null(entry.PayloadJson);
        Assert.Null(entry.IpAddress);
        Assert.Null(entry.UserAgent);
        Assert.InRange(entry.Timestamp, before, after);
    }

    [Fact]
    public void Create_FullArgs_RoundTripsEveryField()
    {
        var user   = Guid.NewGuid();
        var entity = Guid.NewGuid();

        var entry = AuditEntry.Create(
            action:      "AnalyzeListAgainstVacancy",
            userId:      user,
            entityType:  "Vacancy",
            entityId:    entity,
            payloadJson: """{"vacancyId":"abc"}""",
            ipAddress:   "10.0.0.1",
            userAgent:   "Mozilla/5.0",
            outcome:     "Success");

        Assert.Equal(user, entry.UserId);
        Assert.Equal("Vacancy", entry.EntityType);
        Assert.Equal(entity, entry.EntityId);
        Assert.Equal("""{"vacancyId":"abc"}""", entry.PayloadJson);
        Assert.Equal("10.0.0.1", entry.IpAddress);
        Assert.Equal("Mozilla/5.0", entry.UserAgent);
        Assert.Equal("Success", entry.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_BlankAction_Throws(string? blank)
    {
        Assert.Throws<ArgumentException>(() => AuditEntry.Create(
            action:      blank!,
            userId:      null,
            entityType:  null,
            entityId:    null,
            payloadJson: null,
            ipAddress:   null,
            userAgent:   null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhitespaceEntityType_NormalisedToNull(string blank)
    {
        var entry = AuditEntry.Create(
            action:      "X",
            userId:      null,
            entityType:  blank,
            entityId:    null,
            payloadJson: null,
            ipAddress:   null,
            userAgent:   null);

        Assert.Null(entry.EntityType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhitespaceIpAddress_NormalisedToNull(string blank)
    {
        var entry = AuditEntry.Create(
            action:      "X",
            userId:      null,
            entityType:  null,
            entityId:    null,
            payloadJson: null,
            ipAddress:   blank,
            userAgent:   null);

        Assert.Null(entry.IpAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WhitespaceUserAgent_NormalisedToNull(string blank)
    {
        var entry = AuditEntry.Create(
            action:      "X",
            userId:      null,
            entityType:  null,
            entityId:    null,
            payloadJson: null,
            ipAddress:   null,
            userAgent:   blank);

        Assert.Null(entry.UserAgent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankOutcome_FallsBackToSuccess(string? blank)
    {
        var entry = AuditEntry.Create(
            action:      "X",
            userId:      null,
            entityType:  null,
            entityId:    null,
            payloadJson: null,
            ipAddress:   null,
            userAgent:   null,
            outcome:     blank!);

        Assert.Equal("Success", entry.Outcome);
    }

    [Fact]
    public void Create_TimestampIsUtc()
    {
        var entry = AuditEntry.Create(
            action:      "X",
            userId:      null,
            entityType:  null,
            entityId:    null,
            payloadJson: null,
            ipAddress:   null,
            userAgent:   null);

        Assert.Equal(DateTimeKind.Utc, entry.Timestamp.Kind);
    }

    [Fact]
    public void Create_TwoEntries_HaveDistinctIds()
    {
        var a = AuditEntry.Create("X", null, null, null, null, null, null);
        var b = AuditEntry.Create("X", null, null, null, null, null, null);

        Assert.NotEqual(a.Id, b.Id);
    }
}
