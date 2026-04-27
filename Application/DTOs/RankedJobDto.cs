namespace Application.DTOs;

public class RankedJobDto
{
    public Guid JobId { get; init; }
    public float Score { get; init; }
    public string Reasoning { get; init; } = string.Empty;
    public int Rank { get; init; }
}
