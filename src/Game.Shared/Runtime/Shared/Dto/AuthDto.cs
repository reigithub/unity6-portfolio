using System.ComponentModel.DataAnnotations;
using MessagePack;
using Key = MessagePack.KeyAttribute;

namespace Game.Library.Shared.Dto
{
    // ============================================================
    // Request DTOs
    // ============================================================
    [MessagePackObject]
    public class LoginRequest
    {
        [Key(0)]
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class GuestLoginRequest
    {
        [Key(0)]
        [Required]
        [StringLength(255, MinimumLength = 16)]
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class EmailLoginRequest
    {
        [Key(0)]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class VerifyEmailRequest
    {
        [Key(0)]
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class ForgotPasswordRequest
    {
        [Key(0)]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class ResetPasswordRequest
    {
        [Key(0)]
        [Required]
        public string Token { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class LinkEmailRequest
    {
        [Key(0)]
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Key(1)]
        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class UpdateUserRequest
    {
        [Key(0)]
        [StringLength(50, MinimumLength = 2)]
        public string? UserName { get; set; }
    }

    // ============================================================
    // Response DTOs
    // ============================================================
    [MessagePackObject]
    public class LoginResponse
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string UserName { get; set; } = string.Empty;

        [Key(2)]
        public string Token { get; set; } = string.Empty;

        [Key(3)]
        public bool IsNewUser { get; set; }

        [Key(4)]
        public string SigningKey { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class AccountLinkResponse
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string UserName { get; set; } = string.Empty;

        [Key(2)]
        public string Token { get; set; } = string.Empty;

        [Key(3)]
        public string AuthType { get; set; } = string.Empty;

        [Key(4)]
        public string? Email { get; set; }

        [Key(5)]
        public string SigningKey { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class TransferPasswordResponse
    {
        [Key(0)]
        public string TransferPassword { get; set; } = string.Empty;

        [Key(1)]
        public string UserId { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class UserResponse
    {
        [Key(0)]
        public string UserId { get; set; } = string.Empty;

        [Key(1)]
        public string UserName { get; set; } = string.Empty;

        [Key(2)]
        public int Level { get; set; }

        [Key(3)]
        public long RegisteredAt { get; set; }

        [Key(4)]
        public string AuthType { get; set; } = string.Empty;

        [Key(5)]
        public string? Email { get; set; }

        [Key(6)]
        public bool HasTransferPassword { get; set; }
    }
}
