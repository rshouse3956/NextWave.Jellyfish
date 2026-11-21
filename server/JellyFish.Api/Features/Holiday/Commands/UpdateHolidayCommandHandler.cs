using JellyFish.Api.Common;
using JellyFish.Api.Features.Holiday.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed class UpdateHolidayCommandHandler : IRequestHandler<UpdateHolidayCommand, Result<HolidayDto>>
{
    private readonly IHolidayRepository _repo;

    public UpdateHolidayCommandHandler(IHolidayRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<HolidayDto>> Handle(UpdateHolidayCommand request, CancellationToken cancellationToken)
    {
        var get = await _repo.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure<HolidayDto>(get.Error);

        var entity = get.Value;
        entity.Update(request.Category, request.CronExpression, request.Comment, request.IsEnabled);

        var save = await _repo.UpdateAsync(entity, cancellationToken);
        if (save.IsFailure) return Result.Failure<HolidayDto>(save.Error);

        return Result.Success(HolidayMapper.ToDto(entity));
    }
}

