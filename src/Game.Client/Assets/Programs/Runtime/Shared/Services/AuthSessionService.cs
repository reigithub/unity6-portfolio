using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Shared.SaveData;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// セッション管理サービス実装
    /// ISessionSaveDataStorage (MemoryPack、device-bound構成) にトークン・ユーザー情報を保存/復元
    /// </summary>
    public class AuthSessionService : IAuthSessionService
    {
        private const string SaveKey = "session";
        private readonly ISessionSaveDataStorage _storage;
        private SessionSaveData _data;
        private DateTime? _lastRefreshedAt;

        /// <summary>
        /// 冗長な refresh 呼び出しを skip する default 時間閾値。
        /// Server 側 JWT 有効期限 (60 分) の約 0.83% で、安全側に倒した短めの値。
        /// 呼び出し側は <see cref="IsRecentlyRefreshed()"/> 経由で暗黙参照する。
        /// カスタム threshold が必要な特殊ケースは <see cref="IsRecentlyRefreshed(TimeSpan)"/> を使用する。
        /// </summary>
        private readonly TimeSpan _defaultFreshnessThreshold = TimeSpan.FromSeconds(30);

        public AuthSessionService(ISessionSaveDataStorage storage)
        {
            _storage = storage;
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_data?.AuthToken);
        public string AuthToken => _data?.AuthToken;
        public string RefreshToken => _data?.RefreshToken;
        public string UserId => _data?.UserId;
        public string UserName => _data?.UserName;
        public string AuthType => _data?.AuthType;
        public string SigningKey => _data?.SigningKey;

        public DateTime? LastRefreshedAt => _lastRefreshedAt;

        public bool IsRecentlyRefreshed() => IsRecentlyRefreshed(_defaultFreshnessThreshold);

        public bool IsRecentlyRefreshed(TimeSpan threshold)
        {
            if (_lastRefreshedAt == null) return false;

            var elapsed = DateTime.UtcNow - _lastRefreshedAt.Value;

            // 時計巻き戻し (NTP sync 等) 時は safe side (skip しない) に倒す
            if (elapsed < TimeSpan.Zero) return false;

            return elapsed < threshold;
        }

        public void MarkRefreshed()
        {
            _lastRefreshedAt = DateTime.UtcNow;
        }

        public async UniTask SaveSessionAsync(LoginResponse response, string authType = "guest")
        {
            _data ??= new SessionSaveData();
            _data.AuthToken = response.Token;
            _data.RefreshToken = response.RefreshToken;
            _data.UserId = response.UserId;
            _data.UserName = response.UserName;
            _data.AuthType = authType;
            _data.SigningKey = response.SigningKey;
            await _storage.SaveAsync(SaveKey, _data);
        }

        public async UniTask<bool> RestoreSessionAsync()
        {
            _data = await _storage.LoadAsync<SessionSaveData>(SaveKey);
            if (_data == null || string.IsNullOrEmpty(_data.AuthToken))
            {
                Debug.Log($"[DIAG] RestoreSessionAsync: no session found (data null={_data == null}, path={_storage.BasePath})");
                _data ??= new SessionSaveData();
                return false;
            }
            Debug.Log($"[DIAG] RestoreSessionAsync: restored userId={_data.UserId}, userName={_data.UserName}, fingerprint={_data.DeviceFingerprint}, path={_storage.BasePath}");
            return true;
        }

        public async UniTask ClearSessionAsync()
        {
            _data ??= new SessionSaveData();
            var fingerprint = _data.DeviceFingerprint;
            _data = new SessionSaveData { DeviceFingerprint = fingerprint };
            _lastRefreshedAt = null;
            await _storage.SaveAsync(SaveKey, _data);
        }

        public async UniTask<string> GetOrCreateDeviceFingerprintAsync()
        {
            _data ??= new SessionSaveData();
            if (!string.IsNullOrEmpty(_data.DeviceFingerprint))
            {
                Debug.Log($"[DIAG] GetOrCreateDeviceFingerprintAsync: reused existing fingerprint={_data.DeviceFingerprint}, path={_storage.BasePath}");
                return _data.DeviceFingerprint;
            }
            _data.DeviceFingerprint = GenerateDeviceFingerprint();
            Debug.Log($"[DIAG] GetOrCreateDeviceFingerprintAsync: generated new fingerprint={_data.DeviceFingerprint}, path={_storage.BasePath}");
            await _storage.SaveAsync(SaveKey, _data);
            return _data.DeviceFingerprint;
        }

        public string FormatUserId()
        {
            if (string.IsNullOrEmpty(UserId) || UserId.Length != 12)
            {
                return UserId ?? "";
            }

            return $"{UserId.Substring(0, 4)} {UserId.Substring(4, 4)} {UserId.Substring(8)}";
        }

        public async UniTask SaveTransferPasswordAsync(string password)
        {
            _data ??= new SessionSaveData();
            _data.TransferPassword = password;
            await _storage.SaveAsync(SaveKey, _data);
        }

        public string GetTransferPassword()
        {
            return _data?.TransferPassword;
        }

        public async UniTask ClearTransferPasswordAsync()
        {
            if (_data != null)
            {
                _data.TransferPassword = null;
                await _storage.SaveAsync(SaveKey, _data);
            }
        }

        private static string GenerateDeviceFingerprint()
        {
            // SystemInfo + GUID で一意なフィンガープリントを生成
            var raw = $"{SystemInfo.deviceUniqueIdentifier}_{Guid.NewGuid():N}";
            return raw;
        }
    }
}
