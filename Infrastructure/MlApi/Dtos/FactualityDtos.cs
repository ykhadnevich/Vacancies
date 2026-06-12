using System.Text.Json.Serialization;

namespace Infrastructure.MlApi.Dtos;


internal sealed record FactualityCheckRequestDto(
    [property: JsonPropertyName("document")] string Document,
    [property: JsonPropertyName("claims")] IReadOnlyList<string> Claims);


internal sealed record FactualityResultItemDto(
    [property: JsonPropertyName("claim")] string Claim,
    [property: JsonPropertyName("label")] int Label,
    [property: JsonPropertyName("score")] double Score);


internal sealed record FactualityCheckResponseDto(
    [property: JsonPropertyName("results")] IReadOnlyList<FactualityResultItemDto> Results,
    [property: JsonPropertyName("model_version")] string ModelVersion);
