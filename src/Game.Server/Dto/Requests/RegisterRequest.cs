using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class RegisterRequest
{
    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}
