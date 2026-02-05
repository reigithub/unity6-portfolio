using System.ComponentModel.DataAnnotations;

namespace Game.Server.Dto.Requests;

public class UpdateUserRequest
{
    [StringLength(50, MinimumLength = 2)]
    public string? UserName { get; set; }
}
