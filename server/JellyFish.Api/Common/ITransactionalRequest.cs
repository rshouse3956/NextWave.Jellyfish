using MediatR;

namespace JellyFish.Api.Common;

// Marker for commands that should be wrapped in a Unit of Work
public interface ITransactionalRequest { }

