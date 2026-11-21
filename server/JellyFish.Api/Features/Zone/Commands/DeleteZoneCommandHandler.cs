using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed class DeleteZoneCommandHandler : IRequestHandler<DeleteZoneCommand, Result>
{
    private readonly IZoneRepository _zones;

    public DeleteZoneCommandHandler(IZoneRepository zones)
    {
        _zones = zones;
    }

    public async Task<Result> Handle(DeleteZoneCommand request, CancellationToken cancellationToken)
    {
        var get = await _zones.GetByNameAsync(request.Name, cancellationToken);
        if (get.IsFailure) return Result.Failure(get.Error);

        if (get.Value.IsReadOnly)
            return Result.Failure(Error.Conflict($"Zone '{get.Value.Name}' is read-only"));

        return await _zones.DeleteAsync(request.Name, cancellationToken);
    }
}

