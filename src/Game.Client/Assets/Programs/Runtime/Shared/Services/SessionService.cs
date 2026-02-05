using System;
using Cysharp.Threading.Tasks;
using Game.Shared.Dto.Auth;
using Game.Shared.SaveData;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// セッション管理サービス実装
    /// ISaveDataStorage (MemoryPack) にトークン・ユーザー情報を保存/復元
    /// </summary>
    public class SessionService : ISessionService
    {
        private const string SaveKey = "session";
        private readonly ISaveDataStorage _storage;
        private SessionSaveData _data;

        public SessionService(ISaveDataStorage storage)
        {
            _storage = storage;
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(_data?.AuthToken);
        public string AuthToken => _data?.AuthToken;
        public string UserId => _data?.UserId;
        public string UserName => _data?.UserName;
        public string AuthType => _data?.AuthType;

        public async UniTask SaveSessionAsync(LoginResponse response, string authType = "guest")
        {
            _data ??= new SessionSaveData();
            _data.AuthToken = response.token;
            _data.UserId = response.userId;
            _data.UserName = response.userName;
            _data.AuthType = authType;
            await _storage.SaveAsync(SaveKey, _data);
        }

        public async UniTask<bool> RestoreSessionAsync()
        {
            _data = await _storage.LoadAsync<SessionSaveData>(SaveKey);
            if (_data == null || string.IsNullOrEmpty(_data.AuthToken))
            {
                _data ??= new SessionSaveData();
                return false;
            }
            return true;
        }

        public async UniTask ClearSessionAsync()
        {
            _data ??= new SessionSaveData();
            var fingerprint = _data.DeviceFingerprint;
            _data = new SessionSaveData { DeviceFingerprint = fingerprint };
            await _storage.SaveAsync(SaveKey, _data);
        }

        public async UniTask<string> GetOrCreateDeviceFingerprintAsync()
        {
            _data ??= new SessionSaveData();
            if (!string.IsNullOrEmpty(_data.DeviceFingerprint))
                return _data.DeviceFingerprint;
            _data.DeviceFingerprint = GenerateDeviceFingerprint();
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

        private static string GenerateDeviceFingerprint()
        {
            // SystemInfo + GUID で一意なフィンガープリントを生成
            var raw = $"{SystemInfo.deviceUniqueIdentifier}_{Guid.NewGuid():N}";
            return raw;
        }
    }
}
