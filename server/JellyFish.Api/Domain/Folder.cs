using JellyFish.Api.Common;

namespace JellyFish.Api.Domain;

public sealed class Folder : BaseEntity
{
    private Folder() { }

    public string Name { get; private set; } = string.Empty;
    public bool IsReadOnly { get; private set; }

    public static Result<Folder> Create(string name, bool isReadOnly)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Folder>(Error.Validation("Name is required"));

        var folder = new Folder
        {
            Name = name.Trim(),
            IsReadOnly = isReadOnly
        };

        return Result.Success(folder);
    }

    public void Update(bool isReadOnly)
    {
        IsReadOnly = isReadOnly;
        Touch();
    }
}

