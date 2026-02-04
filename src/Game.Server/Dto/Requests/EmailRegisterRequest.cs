using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class EmailRegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 2)]
    public string UserName { get; set; } = string.Empty;
}
