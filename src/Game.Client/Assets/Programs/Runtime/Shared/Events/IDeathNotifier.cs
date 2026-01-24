using R3;

namespace Game.Shared.Events
{
    /// <summary>
    /// 死亡イベントを通知するインターフェース
    /// </summary>
    public interface IDeathNotifier
    {
        /// <summary>
        /// 死亡時に発火するイベント
        /// </summary>
        Observable<DeathEventData> OnDeathEvent { get; }
    }
}
