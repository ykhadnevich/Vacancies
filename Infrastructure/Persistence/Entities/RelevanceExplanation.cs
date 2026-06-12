namespace Infrastructure.Persistence.Entities;


public class RelevanceExplanation
{
    public Guid CvVersionId { get; set; }
    public Guid JobId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public float Score { get; set; }
    public DateTime GeneratedAt { get; set; }
}
