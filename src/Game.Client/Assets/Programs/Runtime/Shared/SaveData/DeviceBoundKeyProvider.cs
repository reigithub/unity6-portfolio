using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// デバイス固有ID（SystemInfo.deviceUniqueIdentifier）を鍵材料とする鍵導出プロバイダー
    /// 同一デバイス内でのみ復号可能なため、Steam Cloud等によるデバイス間セーブ共有には非対応
    /// （非可搬。deviceId変化時は別データ扱いとなる）
    /// </summary>
    public sealed class DeviceBoundKeyProvider : SaveDataKeyProviderBase
    {
        private const byte LatestSaltVersion = 1;

        // Salt世代管理: ローテーション時はキーをインクリメントして追加し、既存エントリは互換性のため変更しないこと
        private static readonly IReadOnlyDictionary<byte, string> _saltVersionMap = new Dictionary<byte, string>
        {
            { 1, "ivE4fc00X5E1F11UvdCme78C32zEOsL84XLK775TgS4=" },
        };

        private readonly string _deviceId;

        /// <summary>
        /// コンストラクタ。SystemInfoへのアクセスはメインスレッド制約があるため、DI構築時（メインスレッド）に取得する
        /// </summary>
        public DeviceBoundKeyProvider()
        {
            _deviceId = SystemInfo.deviceUniqueIdentifier;
        }

        /// <inheritdoc/>
        public override byte KeySourceId => 0x01;

        /// <inheritdoc/>
        public override byte CurrentSaltVersion => LatestSaltVersion;

        /// <inheritdoc/>
        protected override IReadOnlyDictionary<byte, string> SaltVersions => _saltVersionMap;

        /// <inheritdoc/>
        protected override string SecretMaterial => _deviceId;
    }
}
