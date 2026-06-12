namespace Infrastructure.MlApi.Dtos;


public record EmbedRequestDto(List<string> Texts, string TextType);


public record EmbedResponseDto(List<List<float>> Embeddings, int Dim, string ModelVersion);
