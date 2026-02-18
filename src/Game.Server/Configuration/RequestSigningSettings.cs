using System.ComponentModel.DataAnnotations;

namespace Game.Server.Configuration;

public class RequestSigningSettings
{
    [Required(AllowEmptyStrings = false)]
    [MinLength(32, ErrorMessage = "RequestSigning SecretKey must be at least 32 characters long.")]
    public string SecretKey { get; set; } = "";

    public bool Enabled { get; set; } = true;
}
