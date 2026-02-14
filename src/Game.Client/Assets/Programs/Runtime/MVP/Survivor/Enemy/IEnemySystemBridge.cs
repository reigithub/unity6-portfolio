using Cysharp.Threading.Tasks;
using Game.MVP.Survivor.Services;
using Game.Shared.Events;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// ECS敵システムブリッジのインターフェース
    /// Game.MVP.Survivor側から参照可能にし、循環参照を回避
    /// EcsEnemyBridge (Game.MVP.Survivor.ECS) がこれを実装する
    /// </summary>
    public interface IEnemySystemBridge : IDeathNotifier
    {
        /// <summary>
        /// プレイヤーTransformを設定
        /// </summary>
        void SetPlayer(Transform player);

        /// <summary>
        /// ウェーブマネージャーと接続して初期化
        /// </summary>
        UniTask InitializeAsync(SurvivorStageWaveManager waveManager);

        /// <summary>
        /// 全ての敵をクリア
        /// </summary>
        void ClearAllEnemies();
    }
}
