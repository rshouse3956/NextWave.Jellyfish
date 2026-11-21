using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Commands;

public sealed class DeleteScheduleCommandHandler : IRequestHandler<DeleteScheduleCommand, Result>
{
    private readonly IScheduleRepository _schedules;

    public DeleteScheduleCommandHandler(IScheduleRepository schedules)
    {
        _schedules = schedules;
    }

    public async Task<Result> Handle(DeleteScheduleCommand request, CancellationToken cancellationToken)
    {
        var del = await _schedules.DeleteAsync(request.Name, cancellationToken);
        return del;
    }
}

