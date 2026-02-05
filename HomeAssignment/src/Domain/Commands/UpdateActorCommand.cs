namespace HomeAssignment.Domain.Commands;

public record UpdateActorCommand(
    int ActorId,
    string Name,
    int Rank,
    string Source,
    string CorrelationId);
