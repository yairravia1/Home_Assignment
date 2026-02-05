namespace HomeAssignment.Domain.Models;

public record ActorRecord(
    string ExternalId,
    string Name,
    int? Rank,
    string Source);