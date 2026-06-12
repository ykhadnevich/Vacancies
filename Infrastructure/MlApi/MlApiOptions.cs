namespace Infrastructure.MlApi;

public class MlApiOptions
{
    public const string SectionName = "MlApi";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 2;
}
