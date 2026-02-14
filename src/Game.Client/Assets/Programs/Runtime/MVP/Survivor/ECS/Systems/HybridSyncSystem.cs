using Unity.Entities;
using Unity.Transforms;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// ECS LocalTransform → GameObject.transform に位置・回転を同期するシステム
    /// マネージドシステム（GameObject参照が必要なため）
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class HybridSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities
                .WithAll<EnemyAliveTag>()
                .WithoutBurst()
                .ForEach((in LocalTransform localTransform, in ManagedGameObjectReference managedRef) =>
                {
                    if (managedRef.GameObject == null)
                        return;

                    var goTransform = managedRef.GameObject.transform;
                    goTransform.position = localTransform.Position;
                    goTransform.rotation = localTransform.Rotation;
                })
                .Run();
        }
    }
}
