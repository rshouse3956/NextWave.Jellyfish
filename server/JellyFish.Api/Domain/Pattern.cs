using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class Pattern : BaseEntity
{
    private Pattern() { }

    public string FolderName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsReadOnly { get; private set; }

    public static Result<Pattern> Create(string folderName, string name, bool isReadOnly)
    {
        if (string.IsNullOrWhiteSpace(folderName))
            return Result.Failure<Pattern>(Error.Validation("FolderName is required"));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Pattern>(Error.Validation("Name is required"));

        var p = new Pattern
        {
            FolderName = folderName.Trim(),
            Name = name.Trim(),
            IsReadOnly = isReadOnly
        };
        return Result.Success(p);
    }

    public void Update(bool isReadOnly)
    {
        IsReadOnly = isReadOnly;
        Touch();
    }
}

