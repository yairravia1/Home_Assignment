using HomeAssignment.Domain.Models;

namespace HomeAssignment.Domain.Events;

public record ActorChangedEvent(
    int ActorId,
    ActorChangeType ChangeType,
    ActorSnapshot Actor,
    DateTimeOffset OccurredAt);
