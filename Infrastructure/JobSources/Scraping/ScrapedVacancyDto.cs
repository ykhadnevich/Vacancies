using System.Text.Json.Serialization;

namespace Infrastructure.JobSources.Scraping;


public class ScrapedVacancyDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("search_queries")]
    public List<string> SearchQueries { get; set; } = new();

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("raw_text")]
    public string? RawText { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "unknown";

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("scraped_at")]
    public DateTime ScrapedAt { get; set; }
}
