using System;

namespace Game.Shared.Dto.Auth
{
    // ============================================================
    // Request DTOs
    // ============================================================

    [Serializable]
    public class GuestLoginRequest
    {
        public string deviceFingerprint;
    }

    [Serializable]
    public class EmailRegisterRequest
    {
        public string email;
        public string password;
        public string displayName;
    }

    [Serializable]
    public class EmailLoginRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class VerifyEmailRequest
    {
        public string token;
    }

    [Serializable]
    public class ForgotPasswordRequest
    {
        public string email;
    }

    [Serializable]
    public class ResetPasswordRequest
    {
        public string token;
        public string newPassword;
    }

    // ============================================================
    // Response DTOs
    // ============================================================

    [Serializable]
    public class LoginResponse
    {
        public string userId;
        public string displayName;
        public string token;
        public bool isNewUser;
    }

    [Serializable]
    public class MessageResponse
    {
        public string message;
    }
}
