using JellyFish.Api.Common;
using MediatR;

namespace JellyFish.Api.Features.JellyfishController.Commands;

public sealed record SyncAllPatternDetailsFromControllerCommand : IRequest<Result<SyncPatternDetailsSummaryDto>>, ITransactionalRequest;

/// <summary>
/// Outcome of syncing every pattern's details. Patterns that sync are saved even when others fail.
/// </summary>
/// <param name="Total">Patterns on the controller (after removing duplicates).</param>
/// <param name="Synced">Patterns whose details were saved.</param>
/// <param name="NoDetails">"Folder/Name" of patterns the controller returned no details for.</param>
/// <param name="Failed">Patterns that could not be synced, with the reason.</param>
public sealed record SyncPatternDetailsSummaryDto(
    int Total,
    int Synced,
    IReadOnlyList<string> NoDetails,
    IReadOnlyList<SyncPatternFailureDto> Failed);

public sealed record SyncPatternFailureDto(string FolderName, string Name, string Error);
