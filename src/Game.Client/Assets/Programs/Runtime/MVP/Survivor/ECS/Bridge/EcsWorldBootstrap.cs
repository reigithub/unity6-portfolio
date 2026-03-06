using Unity.Entities;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// ECS Worldの生成・破棄とシステムの登録を管理
    /// デフォルトWorldの自動生成を抑止し、必要時にのみ専用Worldを生成する
    /// </summary>
    public static class EcsWorldBootstrap
    {
        private static World _ecsWorld;

        /// <summary>ECS World名</summary>
        public const string WorldName = "SurvivorECSWorld";

        /// <summary>現在のECS World（未生成時はnull）</summary>
        public static World World => _ecsWorld;

        /// <summary>Worldが有効かどうか</summary>
        public static bool IsWorldActive => _ecsWorld != null && _ecsWorld.IsCreated;

        /// <summary>
        /// ECS Worldを生成し、必要なシステムを登録
        /// </summary>
        public static World CreateWorld()
        {
            if (IsWorldActive)
            {
                UnityEngine.Debug.LogWarning($"[EcsWorldBootstrap] World '{WorldName}' already exists.");
                return _ecsWorld;
            }

            _ecsWorld = new World(WorldName);

            // システムグループ
            var simGroup = _ecsWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var presentationGroup = _ecsWorld.GetOrCreateSystemManaged<PresentationSystemGroup>();

            // Burst並列システム
            var aiStateSystem = _ecsWorld.GetOrCreateSystem<EnemyAIStateSystem>();
            var movementSystem = _ecsWorld.GetOrCreateSystem<EnemyMovementSystem>();
            var damageSystem = _ecsWorld.GetOrCreateSystem<EnemyDamageSystem>();

            // マネージドシステム
            var playerPosSystem = _ecsWorld.GetOrCreateSystemManaged<PlayerPositionUpdateSystem>();
            var spawnSystem = _ecsWorld.GetOrCreateSystemManaged<SpawnProcessSystem>();
            var steeringSystem = _ecsWorld.GetOrCreateSystemManaged<EnemySteeringSystem>();
            var deathCleanupSystem = _ecsWorld.GetOrCreateSystemManaged<EnemyDeathCleanupSystem>();

            // SimulationSystemGroupに追加（順序制御）
            simGroup.AddSystemToUpdateList(playerPosSystem);
            simGroup.AddSystemToUpdateList(spawnSystem);
            simGroup.AddSystemToUpdateList(aiStateSystem);
            simGroup.AddSystemToUpdateList(steeringSystem);  // AIState → Steering → Movement
            simGroup.AddSystemToUpdateList(movementSystem);
            simGroup.AddSystemToUpdateList(damageSystem);
            simGroup.AddSystemToUpdateList(deathCleanupSystem);

            simGroup.SortSystems();

            // PresentationSystemGroup: HybridSyncSystem登録
            var hybridSyncSystem = _ecsWorld.GetOrCreateSystemManaged<HybridSyncSystem>();
            presentationGroup.AddSystemToUpdateList(hybridSyncSystem);
            presentationGroup.SortSystems();

            UnityEngine.Debug.Log($"[EcsWorldBootstrap] World '{WorldName}' created with all systems.");
            return _ecsWorld;
        }

        /// <summary>
        /// ECS Worldを破棄
        /// </summary>
        public static void DestroyWorld()
        {
            if (!IsWorldActive)
                return;

            _ecsWorld.Dispose();
            _ecsWorld = null;

            UnityEngine.Debug.Log($"[EcsWorldBootstrap] World '{WorldName}' destroyed.");
        }

        /// <summary>
        /// 指定したマネージドシステムを取得
        /// </summary>
        public static T GetSystem<T>() where T : SystemBase
        {
            if (!IsWorldActive)
                return null;

            return _ecsWorld.GetExistingSystemManaged<T>();
        }
    }
}
