using System.ComponentModel.DataAnnotations;

namespace HomeAssignment.Api.Dtos.Actor;

public class UpdateActorRequestDto
{
    [Required(ErrorMessage = "Actor name is required")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 20 characters")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 2000, ErrorMessage = "Rank must be between 1 and 2000")]
    public int Rank { get; set; }

    [Required(ErrorMessage = "Source is required")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Source must be between 2 and 20 characters")]
    public string Source { get; set; } = string.Empty;
}

