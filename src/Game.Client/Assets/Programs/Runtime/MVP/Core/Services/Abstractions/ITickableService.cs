using System;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// 動的なTick登録を管理するサービスインターフェース
    /// VContainerのPlayerLoop統合を利用し、ITickable/IFixedTickable/ILateTickableの
    /// タイミングでActionを実行する
    /// </summary>
    /// <example>
    /// // Update タイミングで実行
    /// _tickableService.Register&lt;ITickable&gt;(OnTick);
    ///
    /// // FixedUpdate タイミングで実行
    /// _tickableService.Register&lt;IFixedTickable&gt;(OnFixedTick);
    ///
    /// // LateUpdate タイミングで実行
    /// _tickableService.Register&lt;ILateTickable&gt;(OnLateTick);
    /// </example>
    public interface ITickableService
    {
        /// <summary>
        /// 指定したタイミングで実行されるActionを登録
        /// </summary>
        /// <typeparam name="T">ITickable, IFixedTickable, ILateTickable のいずれか</typeparam>
        /// <param name="action">実行するAction</param>
        void Register<T>(Action action) where T : class;

        /// <summary>
        /// 登録済みのActionを解除
        /// </summary>
        /// <typeparam name="T">ITickable, IFixedTickable, ILateTickable のいずれか</typeparam>
        /// <param name="action">解除するAction</param>
        void Unregister<T>(Action action) where T : class;
    }
}
