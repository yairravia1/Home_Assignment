namespace HomeAssignment.Domain.Models;

/// <summary>
/// Domain model for updating an actor.
/// API request DTOs should map into this type.
/// </summary>
public sealed record ActorUpdate(
    string Name,
    int Rank,
    string Source);

