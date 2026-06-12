namespace Application.Common.Scoring;


public sealed record RoleBucketMapping(
    string JobRolePattern,
    RoleBucketId Bucket,
    string? Note = null);
