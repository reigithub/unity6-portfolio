using System.ComponentModel.DataAnnotations;
using Game.Server.Shared.Configuration;

namespace Game.Server.Configuration;

public class JwtSettings : JwtValidationSettings
{
    [Range(1, int.MaxValue)]
    public int ExpirationMinutes { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int RefreshExpirationDays { get; set; } = 30;
}
