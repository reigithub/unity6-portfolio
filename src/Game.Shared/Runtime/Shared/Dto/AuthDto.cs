using System.ComponentModel.DataAnnotations;
using MessagePack;

namespace Game.Library.Shared.Dto
{
    // ============================================================
    // Request DTOs
    // ============================================================

    [MessagePackObject(true)]
    public class LoginRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class GuestLoginRequest
    {
        [Required]
        [StringLength(255, MinimumLength = 16)]
        public string DeviceFingerprint { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class EmailLoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class VerifyEmailRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class LinkEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class UpdateUserRequest
    {
        [StringLength(50, MinimumLength = 2)]
        public string? UserName { get; set; }
    }

    // ============================================================
    // Response DTOs
    // ============================================================

    [MessagePackObject(true)]
    public class LoginResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public bool IsNewUser { get; set; }

        public string SigningKey { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class AccountLinkResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string Token { get; set; } = string.Empty;

        public string AuthType { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string SigningKey { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class TransferPasswordResponse
    {
        public string TransferPassword { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class UserResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public int Level { get; set; }

        public long RegisteredAt { get; set; }

        public string AuthType { get; set; } = string.Empty;

        public string? Email { get; set; }

        public bool HasTransferPassword { get; set; }
    }
}
