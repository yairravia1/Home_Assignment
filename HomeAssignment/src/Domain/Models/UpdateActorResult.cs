namespace HomeAssignment.Domain.Models;

public class UpdateActorResult
{
    public Actor? Actor { get; init; }
    public bool NotFound { get; init; }
    public bool DuplicateRank { get; init; }
}
