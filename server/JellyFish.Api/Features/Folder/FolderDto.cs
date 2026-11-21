namespace JellyFish.Api.Features.Folder;

public sealed record FolderDto(string Name, DateTime CreateDate, DateTime LastModifiedDate, bool IsReadOnly, bool SyncRequired);

