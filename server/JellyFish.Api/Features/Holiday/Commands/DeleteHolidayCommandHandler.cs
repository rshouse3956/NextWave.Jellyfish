using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed class DeleteHolidayCommandHandler : IRequestHandler<DeleteHolidayCommand, Result>
{
    private readonly IHolidayRepository _repo;

    public DeleteHolidayCommandHandler(IHolidayRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result> Handle(DeleteHolidayCommand request, CancellationToken cancellationToken)
    {
        var get = await _repo.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure(get.Error);

        return await _repo.DeleteAsync(request.Name, cancellationToken);
    }
}

