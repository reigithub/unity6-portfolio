using System;
using System.Text;
using MemoryPack;

namespace Game.Shared.Netcode
{
    /// <summary>
    /// NGO 接続時の ConnectionData ペイロード。
    /// クライアント → サーバーへ stageId とセッショントークンを送信する。
    /// ゲームモード非依存 — 任意のモードで共通利用。
    /// </summary>
    [MemoryPackable]
    public partial class NetworkConnectionPayload
    {
        public int StageId { get; set; }
        public string SessionToken { get; set; }

        /// <summary>stageId とセッショントークンを ConnectionData 用バイト列にエンコードする。</summary>
        public static byte[] Encode(int stageId, string sessionToken = "")
        {
            var payload = new NetworkConnectionPayload
            {
                StageId = stageId,
                SessionToken = sessionToken ?? string.Empty
            };
            return MemoryPackSerializer.Serialize(payload);
        }

        /// <summary>ConnectionData バイト列をデコードする。レガシーフォーマットにも対応。</summary>
        public static (int StageId, string SessionToken) Decode(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return (1, string.Empty);
            }

            try
            {
                var payload = MemoryPackSerializer.Deserialize<NetworkConnectionPayload>(data);
                return (payload.StageId, payload.SessionToken ?? string.Empty);
            }
            catch
            {
                // レガシーフォーマット: UTF-8 トークンのみ → stageId=1 にフォールバック
                return (1, Encoding.UTF8.GetString(data));
            }
        }
    }
}
