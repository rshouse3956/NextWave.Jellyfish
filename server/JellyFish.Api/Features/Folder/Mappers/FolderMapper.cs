namespace JellyFish.Api.Features.Folder.Mappers;

public static class FolderMapper
{
    public static FolderDto ToDto(JellyFish.Api.Domain.Folder entity) => new(entity.Name, entity.CreateDate, entity.LastModifiedDate, entity.IsReadOnly, entity.SyncRequired ?? false);
}

