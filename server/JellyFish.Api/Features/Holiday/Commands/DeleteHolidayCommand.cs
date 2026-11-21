using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.Holiday.Commands;

public sealed record DeleteHolidayCommand(string Name) : IRequest<Result>, ITransactionalRequest;

