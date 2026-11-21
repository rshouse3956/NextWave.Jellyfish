using JellyFish.Api.Common;
using JellyFish.Api.Features.Zone.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Zone.Queries;

public sealed class ListZonesQueryHandler : IRequestHandler<ListZonesQuery, Result<List<ZoneDto>>>
{
    private readonly IZoneRepository _zones;

    public ListZonesQueryHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<Result<List<ZoneDto>>> Handle(ListZonesQuery request, CancellationToken cancellationToken)
    {
        var res = await _zones.ListAsync(cancellationToken);
        if (res.IsFailure) return Result.Failure<List<ZoneDto>>(res.Error);
        return Result.Success(res.Value.Select(ZoneMapper.ToDto).ToList());
    }
}

