using System.Text.Json.Serialization;

namespace Infrastructure.MlApi.Dtos;


public record ReasonRequestDto(string CvText, string JobTitle, string JobDescription, float Score);


public record ReasonResponseDto(
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("latency_ms")] int LatencyMs);
