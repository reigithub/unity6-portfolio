using System;
using Game.Shared.Dto.Auth;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// セッション管理サービス実装
    /// PlayerPrefs にトークン・ユーザー情報を保存/復元
    /// </summary>
    public class SessionService : ISessionService
    {
        private const string KeyAuthToken = "auth_token";
        private const string KeyUserId = "auth_user_id";
        private const string KeyDisplayName = "auth_display_name";
        private const string KeyAuthType = "auth_type";
        private const string KeyDeviceFingerprint = "device_fingerprint";

        public bool IsAuthenticated => !string.IsNullOrEmpty(AuthToken);
        public string AuthToken { get; private set; }
        public string UserId { get; private set; }
        public string DisplayName { get; private set; }
        public string AuthType { get; private set; }

        public void SaveSession(LoginResponse response, string authType = "guest")
        {
            AuthToken = response.token;
            UserId = response.userId;
            DisplayName = response.displayName;
            AuthType = authType;

            PlayerPrefs.SetString(KeyAuthToken, AuthToken);
            PlayerPrefs.SetString(KeyUserId, UserId);
            PlayerPrefs.SetString(KeyDisplayName, DisplayName);
            PlayerPrefs.SetString(KeyAuthType, AuthType);
            PlayerPrefs.Save();
        }

        public bool TryRestoreSession()
        {
            var token = PlayerPrefs.GetString(KeyAuthToken, "");
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            AuthToken = token;
            UserId = PlayerPrefs.GetString(KeyUserId, "");
            DisplayName = PlayerPrefs.GetString(KeyDisplayName, "");
            AuthType = PlayerPrefs.GetString(KeyAuthType, "");

            return true;
        }

        public void ClearSession()
        {
            AuthToken = null;
            UserId = null;
            DisplayName = null;
            AuthType = null;

            PlayerPrefs.DeleteKey(KeyAuthToken);
            PlayerPrefs.DeleteKey(KeyUserId);
            PlayerPrefs.DeleteKey(KeyDisplayName);
            PlayerPrefs.DeleteKey(KeyAuthType);
            PlayerPrefs.Save();
        }

        public string GetOrCreateDeviceFingerprint()
        {
            var fingerprint = PlayerPrefs.GetString(KeyDeviceFingerprint, "");
            if (!string.IsNullOrEmpty(fingerprint))
            {
                return fingerprint;
            }

            fingerprint = GenerateDeviceFingerprint();
            PlayerPrefs.SetString(KeyDeviceFingerprint, fingerprint);
            PlayerPrefs.Save();
            return fingerprint;
        }

        private static string GenerateDeviceFingerprint()
        {
            // SystemInfo + GUID で一意なフィンガープリントを生成
            var raw = $"{SystemInfo.deviceUniqueIdentifier}_{Guid.NewGuid():N}";
            return raw;
        }
    }
}
