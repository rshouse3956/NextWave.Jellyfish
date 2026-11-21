using JellyFish.Api.Common;
using JellyFish.Api.Features.Zone.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Zone.Queries;

public sealed class GetZoneQueryHandler : IRequestHandler<GetZoneQuery, Result<ZoneDto>>
{
    private readonly IZoneRepository _zones;

    public GetZoneQueryHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<Result<ZoneDto>> Handle(GetZoneQuery request, CancellationToken cancellationToken)
    {
        var res = await _zones.GetByNameAsync(request.Name, cancellationToken);
        return res.IsSuccess ? Result.Success(ZoneMapper.ToDto(res.Value)) : Result.Failure<ZoneDto>(res.Error);
    }
}

