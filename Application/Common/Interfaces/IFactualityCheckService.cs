namespace Application.Common.Interfaces;


public interface IFactualityCheckService
{


    Task<IReadOnlyList<FactualityVerdict>> CheckAsync(
        string document,
        IReadOnlyList<string> claims,
        CancellationToken ct = default);
}


public sealed record FactualityVerdict(
    string Claim,
    bool IsSupported,
    double Confidence);
