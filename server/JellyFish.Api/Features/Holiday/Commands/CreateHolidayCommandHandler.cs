using JellyFish.Api.Common;
using JellyFish.Api.Features.Holiday.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed class CreateHolidayCommandHandler : IRequestHandler<CreateHolidayCommand, Result<HolidayDto>>
{
    private readonly IHolidayRepository _repo;

    public CreateHolidayCommandHandler(IHolidayRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<HolidayDto>> Handle(CreateHolidayCommand request, CancellationToken cancellationToken)
    {
        var exists = await _repo.ExistsByNameAsync(request.Name, cancellationToken);
        if (exists)
            return Result.Failure<HolidayDto>(Errors.HolidayErrors.AlreadyExists(request.Name));

        var created = JellyFish.Api.Domain.Holiday.Create(request.Name, request.Category, request.CronExpression, request.Comment, request.IsEnabled);
        if (created.IsFailure) return Result.Failure<HolidayDto>(created.Error);

        var add = await _repo.AddAsync(created.Value, cancellationToken);
        if (add.IsFailure) return Result.Failure<HolidayDto>(add.Error);

        return Result.Success(HolidayMapper.ToDto(created.Value));
    }
}

