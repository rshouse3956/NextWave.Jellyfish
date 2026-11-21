using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed record UpdateScheduleCommand(
    string Name,
    string FolderName,
    string PatternName,
    string Priority,
    string OnTime,
    string OnDay,
    string OffTime,
    string OffDay,
    List<string> ZoneNames
) : IRequest<Result<ScheduleDto>>, ITransactionalRequest;

