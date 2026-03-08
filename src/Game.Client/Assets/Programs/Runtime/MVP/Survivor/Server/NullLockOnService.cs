using Cysharp.Threading.Tasks;
using Game.Shared.Services;
using UnityEngine;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用ロックオンサービス（全メソッドno-op）
    /// TryGetTarget()は常にfalseを返す
    /// </summary>
    public class NullLockOnService : ILockOnService
    {
        public void Initialize(Camera camera, int layer) { }
        public bool HasTarget() => false;

        public bool TryGetTarget(out Transform target, bool autoTarget = true)
        {
            target = null;
            return false;
        }

        public void SetTarget(Vector2 point) { }
        public void ClearTarget() { }
        public void SetAutoTarget(Transform owner) { }
        public void UpdateAutoTarget() { }
        public UniTask PreloadAsync() => UniTask.CompletedTask;
    }
}
