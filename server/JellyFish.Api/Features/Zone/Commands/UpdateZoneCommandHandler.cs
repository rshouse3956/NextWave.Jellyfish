using JellyFish.Api.Common;
using JellyFish.Api.Features.Zone.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed class UpdateZoneCommandHandler : IRequestHandler<UpdateZoneCommand, Result<ZoneDto>>
{
    private readonly IZoneRepository _zones;

    public UpdateZoneCommandHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<Result<ZoneDto>> Handle(UpdateZoneCommand request, CancellationToken cancellationToken)
    {
        var get = await _zones.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure<ZoneDto>(get.Error);

        var entity = get.Value;
        var requestedMap = ZoneMapper.FromDtoEntries(request.ZoneMap);

        if (entity.IsReadOnly)
        {
            // Preserve all attributes except allow toggling IsReadOnly
            entity.Update(entity.CtlrName, request.IsReadOnly, entity.ZoneMap);
        }
        else
        {
            entity.Update(request.CtlrName, request.IsReadOnly, requestedMap);
        }

        entity.MarkChanged();
        var save = await _zones.UpdateAsync(entity, cancellationToken);
        if (save.IsFailure) return Result.Failure<ZoneDto>(save.Error);

        return Result.Success(ZoneMapper.ToDto(entity));
    }
}

