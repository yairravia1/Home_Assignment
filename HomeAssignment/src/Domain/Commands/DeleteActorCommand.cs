namespace HomeAssignment.Domain.Commands;

public record DeleteActorCommand(
    int ActorId,
    string CorrelationId);
