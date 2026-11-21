using JellyFish.Api.Common;
using JellyFish.Api.Features.Holiday.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Queries;

public sealed class GetHolidayQueryHandler : IRequestHandler<GetHolidayQuery, Result<HolidayDto>>
{
    private readonly IHolidayRepository _repo;

    public GetHolidayQueryHandler(IHolidayRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<HolidayDto>> Handle(GetHolidayQuery request, CancellationToken cancellationToken)
    {
        var res = await _repo.GetByNameAsync(request.Name, cancellationToken);
        return res.IsSuccess
            ? Result.Success(HolidayMapper.ToDto(res.Value))
            : Result.Failure<HolidayDto>(res.Error);
    }
}

