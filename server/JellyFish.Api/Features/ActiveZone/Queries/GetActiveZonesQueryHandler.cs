using JellyFish.Api.Common;
using JellyFish.Api.Features.ActiveZone.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.ActiveZone.Queries;

public sealed class GetActiveZonesQueryHandler : IRequestHandler<GetActiveZonesQuery, Result<List<ActiveZoneDto>>>
{
    private readonly IActiveZoneRepository _repo;
    public GetActiveZonesQueryHandler(IActiveZoneRepository repo) { _repo = repo; }

    public async Task<Result<List<ActiveZoneDto>>> Handle(GetActiveZonesQuery request, CancellationToken cancellationToken)
    {
        var list = await _repo.ListAsync(cancellationToken);
        if (list.IsFailure) return Result.Failure<List<ActiveZoneDto>>(list.Error);
        return Result.Success(list.Value.Select(ActiveZoneMapper.ToDto).ToList());
    }
}

