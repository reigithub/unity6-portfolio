namespace Game.Shared.SaveData
{
    /// <summary>
    /// session（認証トークン等）用ストレージの専用型
    /// VContainerで同一インターフェースの複数登録を型で区別するための登録スロットであり、
    /// 鍵構成は合成ルートが注入するISaveDataKeyProviderで決まる
    /// </summary>
    public sealed class SessionSaveDataStorage : EncryptedSaveDataStorage, ISessionSaveDataStorage
    {
        public SessionSaveDataStorage(ISaveDataStorage inner, ISaveDataKeyProvider provider)
            : base(inner, provider)
        {
        }
    }
}
