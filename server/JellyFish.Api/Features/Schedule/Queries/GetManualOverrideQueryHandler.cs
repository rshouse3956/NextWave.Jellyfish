using JellyFish.Api.Common;
using JellyFish.Api.Infrastructure.Services.Scheduling;
using MediatR;

namespace JellyFish.Api.Features.Schedule.Queries;

public sealed class GetManualOverrideQueryHandler : IRequestHandler<GetManualOverrideQuery, Result<ManualOverrideDto?>>
{
    private readonly IManualOverrideService _override;

    public GetManualOverrideQueryHandler(IManualOverrideService @override)
    {
        _override = @override;
    }

    public Task<Result<ManualOverrideDto?>> Handle(GetManualOverrideQuery request, CancellationToken cancellationToken)
    {
        var ov = _override.Current;
        if (ov is null) return Task.FromResult(Result.Success<ManualOverrideDto?>(null));

        var now = DateTime.UtcNow;
        if (ov.Mode == ManualOverrideMode.BlockOff && ov.SuppressUntilUtc.HasValue && ov.SuppressUntilUtc.Value <= now)
        {
            // Expired block-off; clear and return null
            _override.Clear();
            return Task.FromResult(Result.Success<ManualOverrideDto?>(null));
        }

        var dto = new ManualOverrideDto(
            Mode: ov.Mode.ToString(),
            ScheduleName: ov.ScheduleName,
            FolderName: ov.FolderName,
            PatternName: ov.PatternName,
            SetAtUtc: ov.SetAtUtc,
            SuppressUntilUtc: ov.SuppressUntilUtc
        );
        return Task.FromResult(Result.Success<ManualOverrideDto?>(dto));
    }
}

