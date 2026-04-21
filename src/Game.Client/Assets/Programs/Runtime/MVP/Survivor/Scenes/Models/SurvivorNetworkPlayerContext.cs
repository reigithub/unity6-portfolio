using System;
using Fusion;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Network.Survivor;

namespace Game.MVP.Survivor.Scenes.Models
{
    /// <summary>
    /// サーバーサイドの 1 プレイヤー分のゲーム進行状態を束ねる POCO。
    /// <see cref="SurvivorNetworkStageScene"/> が <see cref="System.Collections.Generic.Dictionary{PlayerRef, SurvivorNetworkPlayerContext}"/>
    /// で保持し、per-player の HP/EXP/Level/武器/貢献度/レベルアップ予約数をまとめる。
    /// セッション共有状態 (Wave / GameTime / StageMaster) は <see cref="SurvivorNetworkStageModel"/> が担う。
    /// PR2 時点では 1 エントリのみ運用、PR3 以降で複数プレイヤー対応 + <see cref="IDisposable"/> 所有権の正規化を行う。
    /// </summary>
    public class SurvivorNetworkPlayerContext : IDisposable
    {
        public PlayerRef Player { get; }
        public string UserId { get; }

        /// <summary>Spawn 後に紐付けられるプレイヤーコントローラー</summary>
        public SurvivorPlayerController Controller { get; set; }

        /// <summary>Spawn 後に紐付けられる Fusion NetworkBehaviour</summary>
        public SurvivorFusionPlayer FusionPlayer { get; set; }

        /// <summary>プレイヤー個別のステージモデル (HP/EXP/Level/Score/Kills 等)</summary>
        public SurvivorStageModel StageModel { get; }

        /// <summary>プレイヤー個別のサーバー武器マネージャー</summary>
        public SurvivorNetworkWeaponManager WeaponManager { get; }

        /// <summary>レベルアップ予約数 (State Machine がデクリメントして LevelUp ステートへ遷移)</summary>
        public int PendingLevelUpCount { get; set; }

        /// <summary>死亡済みフラグ (複数プレイヤー時の全滅判定で使用)</summary>
        public bool IsDead { get; set; }

        public SurvivorNetworkPlayerContext(
            PlayerRef player,
            string userId,
            SurvivorStageModel stageModel,
            SurvivorNetworkWeaponManager weaponManager)
        {
            Player = player;
            UserId = userId ?? string.Empty;
            StageModel = stageModel ?? throw new ArgumentNullException(nameof(stageModel));
            WeaponManager = weaponManager ?? throw new ArgumentNullException(nameof(weaponManager));
        }

        /// <summary>
        /// Context 参照をクリアする。
        /// PR2 時点では <see cref="StageModel"/> / <see cref="WeaponManager"/> は VContainer Scoped で管理されるため、
        /// モデル自体の Dispose は呼ばない (二重 Dispose 防止)。PR3 で Transient 化した際に所有者となる。
        /// </summary>
        public void Dispose()
        {
            Controller = null;
            FusionPlayer = null;
        }
    }
}
