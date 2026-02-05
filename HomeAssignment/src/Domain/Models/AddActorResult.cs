namespace HomeAssignment.Domain.Models;

public class AddActorResult
{
    public Actor? Actor { get; init; }
    public bool DuplicateRank { get; init; }
}
