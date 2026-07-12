namespace Game.Shared.SaveData
{
    /// <summary>
    /// セッション（認証トークン等）専用ストレージのマーカーインターフェース
    /// device-bound 構成のストレージを型レベルで区別し、誤って portable 構成へ登録される事故を防ぐ
    /// </summary>
    public interface ISessionSaveDataStorage : ISaveDataStorage
    {
    }
}
