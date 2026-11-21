using JellyFish.Api.Common;
using JellyFish.Api.Features.Holiday.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Queries;

public sealed class ListHolidaysQueryHandler : IRequestHandler<ListHolidaysQuery, Result<List<HolidayDto>>>
{
    private readonly IHolidayRepository _repo;

    public ListHolidaysQueryHandler(IHolidayRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<List<HolidayDto>>> Handle(ListHolidaysQuery request, CancellationToken cancellationToken)
    {
        var res = await _repo.ListAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure<List<HolidayDto>>(res.Error);
        return Result.Success(res.Value.Select(HolidayMapper.ToDto).ToList());
    }
}

