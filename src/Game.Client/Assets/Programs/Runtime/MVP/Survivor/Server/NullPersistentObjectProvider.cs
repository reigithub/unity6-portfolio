#if UNITY_SERVER
using Game.Shared.Services;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用永続オブジェクトプロバイダー（全メソッドno-op）
    /// Get&lt;T&gt;()はnullを返す。GameRootControllerの全呼び出し元でnullチェック済み
    /// </summary>
    public class NullPersistentObjectProvider : IPersistentObjectProvider
    {
        public void Register<T>(T instance) where T : class { }
        public T Get<T>() where T : class => null;

        public bool TryGet<T>(out T instance) where T : class
        {
            instance = null;
            return false;
        }

        public void Unregister<T>() where T : class { }
        public void Clear() { }
    }
}
#endif
