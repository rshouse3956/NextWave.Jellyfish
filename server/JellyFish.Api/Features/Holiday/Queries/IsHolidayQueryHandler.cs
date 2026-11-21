using Cronos;
using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Repositories;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Queries;

public sealed class IsHolidayQueryHandler : IRequestHandler<IsHolidayQuery, Result<IsHolidayDto>>
{
    private readonly IHolidayRepository _repo;

    public IsHolidayQueryHandler(IHolidayRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<IsHolidayDto>> Handle(IsHolidayQuery request, CancellationToken cancellationToken)
    {
        var listRes = await _repo.ListAsync(cancellationToken);
        if (listRes.IsFailure) return Result.Failure<IsHolidayDto>(listRes.Error);

        var tz = TimeZoneInfo.Local;
        var startLocal = new DateTime(request.Date.Year, request.Date.Month, request.Date.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);

        var matches = new List<string>();

        foreach (var h in listRes.Value.Where(h => h.IsEnabled))
        {
            CronExpression expr;
            if (!TryParseCron(h.CronExpression, out expr))
                continue;

            var occ = expr.GetOccurrences(fromUtc, toUtc, tz, fromInclusive: true, toInclusive: false);
            if (occ.Any())
                matches.Add(h.Name);
        }

        return Result.Success(new IsHolidayDto(matches.Count > 0, matches));
    }

    private static bool TryParseCron(string expr, out CronExpression parsed)
    {
        try { parsed = CronExpression.Parse(expr); return true; }
        catch { try { parsed = CronExpression.Parse(expr, CronFormat.IncludeSeconds); return true; } catch { parsed = null!; return false; } }
    }
}

