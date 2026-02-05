using System.ComponentModel.DataAnnotations;

namespace HomeAssignment.Api.Queries;

public class ActorQueryObject
{
    [StringLength(200, ErrorMessage = "Actor name must not exceed 200 characters")]
    public string? ActorName { get; set; }

    [Range(1, 1000, ErrorMessage = "MinRank must be between 1 and 1000")]
    public int? MinRank { get; set; }

    [Range(1, 1000, ErrorMessage = "MaxRank must be between 1 and 1000")]
    public int? MaxRank { get; set; }

    [StringLength(50, ErrorMessage = "Provider must not exceed 50 characters")]
    public string? Provider { get; set; }
}

