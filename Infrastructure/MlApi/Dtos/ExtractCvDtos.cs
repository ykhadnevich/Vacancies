using System.Text.Json.Serialization;

namespace Infrastructure.MlApi.Dtos;


public record ExtractCvRequestDto(
    [property: JsonPropertyName("cv_text")] string CvText);


public record ExtractCvResponseDto(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("latency_ms")] int LatencyMs);
