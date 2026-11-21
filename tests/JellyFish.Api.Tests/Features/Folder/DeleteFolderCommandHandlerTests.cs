using Shouldly;
using JellyFish.Api.Common;
using JellyFish.Api.Domain;
using JellyFish.Api.Features.Folder.Commands;
using JellyFish.Api.Infrastructure.Repositories;
using Moq;
using FolderEntity = JellyFish.Api.Domain.Folder;

namespace JellyFish.Api.Tests.Features.Folder;

public class DeleteFolderCommandHandlerTests
{
    [Fact]
    public async Task ReadOnlyFolder_ReturnsConflict()
    {
        var repo = new Mock<IFolderRepository>();
        var folder = FolderEntity.Create("A", true).Value;
        repo.Setup(r => r.GetByNameAsync("A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(folder));

        var sut = new DeleteFolderCommandHandler(repo.Object);
        var res = await sut.Handle(new DeleteFolderCommand("A"), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Fact]
    public async Task NotFound_IsPropagated()
    {
        var repo = new Mock<IFolderRepository>();
        repo.Setup(r => r.GetByNameAsync("X", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<FolderEntity>(Error.NotFound("not found")));

        var sut = new DeleteFolderCommandHandler(repo.Object);
        var res = await sut.Handle(new DeleteFolderCommand("X"), default);

        res.IsFailure.ShouldBeTrue();
        res.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Fact]
    public async Task Delete_Succeeds_WhenWritable()
    {
        var repo = new Mock<IFolderRepository>();
        var folder = FolderEntity.Create("A", false).Value;
        repo.Setup(r => r.GetByNameAsync("A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(folder));
        repo.Setup(r => r.DeleteAsync("A", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var sut = new DeleteFolderCommandHandler(repo.Object);
        var res = await sut.Handle(new DeleteFolderCommand("A"), default);

        res.IsSuccess.ShouldBeTrue();
    }
}

