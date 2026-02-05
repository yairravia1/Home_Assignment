namespace HomeAssignment.Domain.Models;

public class Actor
{
    public int Id { get; set; }
    public string? ExternalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string Source { get; set; } = string.Empty;
}
