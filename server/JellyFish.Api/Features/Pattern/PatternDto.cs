namespace JellyFish.Api.Features.Pattern;

public sealed record PatternDto(string FolderName, string Name, DateTime CreateDate, DateTime LastModifiedDate, bool IsReadOnly, PatternDetailsDto? Details, bool SyncRequired);

