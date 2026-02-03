using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
