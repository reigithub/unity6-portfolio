using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class GuestLoginRequest
{
    [Required]
    [StringLength(255, MinimumLength = 16)]
    public string DeviceFingerprint { get; set; } = string.Empty;
}
