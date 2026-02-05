namespace HomeAssignment.Domain.Queries;

/// <summary>
/// Domain query object for filtering actors.
/// API-specific query DTOs should map into this type.
/// </summary>
public sealed class ActorQuery
{
    public string? ActorName { get; init; }
    public int? MinRank { get; init; }
    public int? MaxRank { get; init; }
    public string? Provider { get; init; }
}

