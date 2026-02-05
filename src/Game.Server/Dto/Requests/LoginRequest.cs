using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class LoginRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
