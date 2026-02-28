using System;
using System.Text;
using MemoryPack;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// Survivor モード用 NGO 接続ペイロード。
    /// クライアント → サーバーへ stageId とセッショントークンを送信する。
    /// </summary>
    [MemoryPackable]
    public partial class NetworkSurvivorConnectionPayload
    {
        public int StageId { get; set; }
        public string SessionToken { get; set; }

        /// <summary>stageId とセッショントークンを ConnectionData 用バイト列にエンコードする。</summary>
        public static byte[] Encode(int stageId, string sessionToken = "")
        {
            var payload = new NetworkSurvivorConnectionPayload
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
                var payload = MemoryPackSerializer.Deserialize<NetworkSurvivorConnectionPayload>(data);
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