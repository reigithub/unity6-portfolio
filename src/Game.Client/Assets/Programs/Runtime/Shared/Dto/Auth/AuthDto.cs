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
        public string userName;
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

    [Serializable]
    public class LinkEmailRequest
    {
        public string email;
        public string password;
        public string userName;
    }

    // ============================================================
    // Response DTOs
    // ============================================================

    [Serializable]
    public class LoginResponse
    {
        public string userId;
        public string userName;
        public string token;
        public bool isNewUser;
    }

    [Serializable]
    public class AccountLinkResponse
    {
        public string userId;
        public string userName;
        public string token;
        public string authType;
        public string email;
    }

    [Serializable]
    public class UserProfileResponse
    {
        public string userId;
        public string userName;
        public int level;
        public long createdAt;
        public string authType;
        public string email;
    }

    [Serializable]
    public class MessageResponse
    {
        public string message;
    }
}
