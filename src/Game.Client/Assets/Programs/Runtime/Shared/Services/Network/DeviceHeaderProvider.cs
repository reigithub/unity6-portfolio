using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Services.Network
{
    /// <summary>
    /// デバイス情報ヘッダーを提供するユーティリティ
    /// アナリティクスやサーバーログ用にクライアント情報を付与
    /// 注: デバイス識別にはISessionService.GetOrCreateDeviceFingerprintAsync()を使用
    /// </summary>
    public static class DeviceHeaderProvider
    {
        /// <summary>
        /// クライアントバージョンヘッダー名
        /// </summary>
        public const string ClientVersionHeader = "X-Client-Version";

        /// <summary>
        /// プラットフォームヘッダー名
        /// </summary>
        public const string PlatformHeader = "X-Platform";

        /// <summary>
        /// デバイスモデルヘッダー名
        /// </summary>
        public const string DeviceModelHeader = "X-Device-Model";

        /// <summary>
        /// OSバージョンヘッダー名
        /// </summary>
        public const string OsVersionHeader = "X-OS-Version";

        /// <summary>
        /// 言語ヘッダー名
        /// </summary>
        public const string LanguageHeader = "X-Language";

        /// <summary>
        /// 基本的なデバイス情報ヘッダーを取得
        /// </summary>
        /// <returns>デバイス情報を含むヘッダーディクショナリ</returns>
        public static Dictionary<string, string> GetBasicHeaders()
        {
            return new Dictionary<string, string>
            {
                { ClientVersionHeader, Application.version },
                { PlatformHeader, GetPlatformName() },
                { LanguageHeader, Application.systemLanguage.ToString() }
            };
        }

        /// <summary>
        /// 認証用の詳細なデバイス情報ヘッダーを取得
        /// サーバーログやアナリティクス用（デバイス識別はリクエストボディのdeviceFingerprintで行う）
        /// </summary>
        /// <returns>認証用デバイス情報を含むヘッダーディクショナリ</returns>
        public static Dictionary<string, string> GetAuthHeaders()
        {
            return new Dictionary<string, string>
            {
                { ClientVersionHeader, Application.version },
                { PlatformHeader, GetPlatformName() },
                { DeviceModelHeader, SystemInfo.deviceModel },
                { OsVersionHeader, SystemInfo.operatingSystem },
                { LanguageHeader, Application.systemLanguage.ToString() }
            };
        }

        /// <summary>
        /// プラットフォーム名を取得
        /// </summary>
        private static string GetPlatformName()
        {
#if UNITY_IOS
            return "iOS";
#elif UNITY_ANDROID
            return "Android";
#elif UNITY_WEBGL
            return "WebGL";
#elif UNITY_STANDALONE_WIN
            return "Windows";
#elif UNITY_STANDALONE_OSX
            return "macOS";
#elif UNITY_STANDALONE_LINUX
            return "Linux";
#else
            return Application.platform.ToString();
#endif
        }
    }
}
