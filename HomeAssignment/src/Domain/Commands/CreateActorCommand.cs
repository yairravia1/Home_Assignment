namespace HomeAssignment.Domain.Commands;

public record CreateActorCommand(
    string Name,
    int Rank,
    string Source,
    string CorrelationId);
