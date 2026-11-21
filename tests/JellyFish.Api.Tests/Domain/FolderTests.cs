using Shouldly;
using JellyFish.Api.Common;
using JellyFish.Api.Domain;

namespace JellyFish.Api.Tests.Domain;

public class FolderTests
{
    [Fact]
    public void Create_EmptyName_ReturnsValidationFailure()
    {
        var res = Folder.Create("", false);
        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Create_Valid_ReturnsSuccess()
    {
        var res = Folder.Create("My Folder", false);
        res.IsSuccess.ShouldBeTrue();
        res.Value.Name.ShouldBe("My Folder");
        res.Value.IsReadOnly.ShouldBeFalse();
    }
}

