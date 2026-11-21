using JellyFish.Api.Common;
using JellyFish.Api.Domain.Services;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed class ListActiveWindowsQueryHandler : IRequestHandler<ListActiveWindowsQuery, Result<List<string>>>
{
    private readonly IScheduleRepository _repo;
    private readonly IScheduleNextEventService _nextSvc;

    public ListActiveWindowsQueryHandler(IScheduleRepository repo, IScheduleNextEventService nextSvc)
    {
        _repo = repo;
        _nextSvc = nextSvc;
    }

    public async Task<Result<List<string>>> Handle(ListActiveWindowsQuery request, CancellationToken cancellationToken)
    {
        var listRes = await _repo.ListAsync(cancellationToken);
        if (listRes.IsFailure) return Result.Failure<List<string>>(listRes.Error);

        var now = DateTime.UtcNow;
        var names = new List<string>();
        foreach (var s in listRes.Value)
        {
            var activeRes = _nextSvc.IsActiveAt(s.OnTime, s.OnDay, s.OffTime, s.OffDay, now);
            if (activeRes.IsFailure) continue;
            if (activeRes.Value) names.Add(s.Name);
        }
        return Result.Success(names);
    }
}

