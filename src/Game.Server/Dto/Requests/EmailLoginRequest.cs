using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class EmailLoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
