using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
