namespace Infrastructure.MlApi.Dtos;


public record ScoreItemDto(Guid JobId, string Title, string Company, string Description);

public record ScoreRequestDto(string UserProfileText, List<ScoreItemDto> Jobs);


public record ScoreResultItemDto(Guid JobId, float Score, string Method);

public record ScoreResponseDto(List<ScoreResultItemDto> Results);
