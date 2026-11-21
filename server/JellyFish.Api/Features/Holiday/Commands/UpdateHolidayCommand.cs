using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed record UpdateHolidayCommand(
    string Name,
    string Category,
    string CronExpression,
    string Comment,
    bool IsEnabled
) : IRequest<Result<HolidayDto>>, ITransactionalRequest;

