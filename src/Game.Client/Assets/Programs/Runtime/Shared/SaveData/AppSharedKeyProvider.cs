using System.Collections.Generic;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// アプリ埋め込みの固定シークレットを鍵材料とする鍵導出プロバイダー（portable / 非デバイス固定）
    /// Steam Cloud等によるデバイス間セーブ共有を可能にするための選択肢として提供する
    /// </summary>
    /// <remarks>
    /// 脅威モデル: このプロバイダーが導出する鍵はアプリバイナリに埋め込まれた固定シークレットに由来するため、
    /// IL2CPPビルドを逆コンパイル・解析すればシークレットを抽出可能であり、同一バイナリを保持する第三者は
    /// 理論上復号できる。したがってこの暗号化は難読化およびHMACによる改竄検知の水準にとどまり、
    /// 機密情報の秘匿を保証するものではない。目的はセーブデータの整合性検証・改変防止であり、
    /// パスワード等の真に機密な情報を保存する用途には使用しないこと。
    /// </remarks>
    public sealed class AppSharedKeyProvider : SaveDataKeyProviderBase
    {
        private const byte LatestSaltVersion = 1;

        // アプリ埋め込み固定シークレット。上記remarksの脅威モデルを参照（機密保護レベルではない）
        private const string AppEmbeddedSecret = "+tnsaxj6VgWMTnu/Uzey7Juh5MxTfwDbgF9vNHThxBA=";

        // Salt世代管理: ローテーション時はキーをインクリメントして追加し、既存エントリは互換性のため変更しないこと
        private static readonly IReadOnlyDictionary<byte, string> _saltVersionMap = new Dictionary<byte, string>
        {
            { 1, "9p5LMx2D3ubQdgyTaxuBezX/xy7LKaaDdBiP5oluB3s=" },
        };

        /// <inheritdoc/>
        public override byte KeySourceId => 0x02;

        /// <inheritdoc/>
        public override byte CurrentSaltVersion => LatestSaltVersion;

        /// <inheritdoc/>
        protected override IReadOnlyDictionary<byte, string> SaltVersions => _saltVersionMap;

        /// <inheritdoc/>
        protected override string SecretMaterial => AppEmbeddedSecret;
    }
}
