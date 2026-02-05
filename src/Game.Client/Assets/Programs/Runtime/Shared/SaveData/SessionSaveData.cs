using MemoryPack;

namespace Game.Shared.SaveData
{
    /// <summary>
    /// セッション情報のセーブデータ
    /// 認証トークン・ユーザー情報・デバイスフィンガープリントを永続化
    /// </summary>
    [MemoryPackable]
    public partial class SessionSaveData
    {
        public int Version { get; set; } = 1;
        public string AuthToken { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string AuthType { get; set; }
        public string DeviceFingerprint { get; set; }
        public string TransferPassword { get; set; }
    }
}
