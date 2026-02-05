namespace HomeAssignment.Domain.Models;

/// <summary>
/// Snapshot payload for integration events.
/// Keep this in the Domain so events don't depend on API DTOs.
/// </summary>
public sealed record ActorSnapshot(
    int Id,
    string Name,
    int Rank,
    string Source);

