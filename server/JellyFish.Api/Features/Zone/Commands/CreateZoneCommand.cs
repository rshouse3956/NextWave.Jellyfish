using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Zone.Commands;

public sealed record CreateZoneCommand(string Name, string CtlrName, bool IsReadOnly, List<ZoneMapEntryDto> ZoneMap)
    : IRequest<Result<ZoneDto>>, ITransactionalRequest;

