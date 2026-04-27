namespace Domain.ValueObjects;

public sealed class Salary
{
    public decimal? MinAmount { get; }
    public decimal? MaxAmount { get; }
    public string? Currency { get; }
    public string? RawText { get; }

    private Salary() { }

    public Salary(string rawText)
    {
        RawText = rawText;
    }

    public Salary(decimal? min, decimal? max, string currency)
    {
        MinAmount = min;
        MaxAmount = max;
        Currency = currency;
        RawText = max.HasValue
            ? $"{min}-{max} {currency}"
            : $"{min} {currency}";
    }

    public override string ToString() => RawText ?? "Не вказано";
}