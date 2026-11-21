using JellyFish.Api.Common;
using JellyFish.Api.Features.Zone.Mappers;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed class CreateZoneCommandHandler : IRequestHandler<CreateZoneCommand, Result<ZoneDto>>
{
    private readonly IZoneRepository _zones;

    public CreateZoneCommandHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<Result<ZoneDto>> Handle(CreateZoneCommand request, CancellationToken cancellationToken)
    {
        var already = await _zones.ExistsByNameAsync(request.Name, cancellationToken);
        if (already) return Result.Failure<ZoneDto>(Error.Conflict($"Zone '{request.Name}' already exists"));

        var map = ZoneMapper.FromDtoEntries(request.ZoneMap);
        var created = JellyFish.Api.Domain.Zone.Create(request.Name, request.CtlrName, request.IsReadOnly, map);
        if (created.IsFailure) return Result.Failure<ZoneDto>(created.Error);

        created.Value.MarkChanged();
        var add = await _zones.AddAsync(created.Value, cancellationToken);
        if (add.IsFailure) return Result.Failure<ZoneDto>(add.Error);

        return Result.Success(ZoneMapper.ToDto(created.Value));
    }
}

