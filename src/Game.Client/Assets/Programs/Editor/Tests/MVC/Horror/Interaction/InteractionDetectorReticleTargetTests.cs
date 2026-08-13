using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Horror.Interaction;
using Game.Shared.Constants;
using Game.Shared.Enums;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror.Interaction
{
    /// <summary>
    /// <see cref="InteractionDetector.FindReticleTarget"/> の直撃対象特定検証。
    /// 「レティクル直撃は AABB 貫通でなく実コライダーへのヒットで決まる」仕様
    /// （開いた扉等、合成 AABB が実体より大きい対象の誤直撃防止）をここで固定する。
    /// 実コライダーへの Raycast を使うため、生成後に <see cref="Physics.SyncTransforms"/> で
    /// ブロードフェーズを同期する（Raycast はシミュレーション不要）。エディタで開いている
    /// シーンのコライダーとの干渉を避けるため、テストリグは遠隔座標に配置する。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorReticleTargetTests
    {
        // 開いているシーンのレベルジオメトリと交差しない遠隔座標をレイの始点（カメラ相当）とする
        //（InteractionDetectorOcclusionTests の (5000,...) とは別の座標で干渉を避ける）
        private static readonly Vector3 CameraPos = new(6000f, 6000f, 6000f);

        private static readonly Ray CenterRay = new(CameraPos, Vector3.forward);

        private static readonly int InteractableMask = LayerMaskConstants.Interactable;

        // 実運用の _discoverRadius 相当
        private const float MaxDistance = 3f;

        private readonly List<GameObject> _spawned = new();
        private RaycastHit[] _hitBuffer;

        [SetUp]
        public void SetUp()
        {
            _hitBuffer = new RaycastHit[16];
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
        }

        private IInteractable Find(out Vector3 hitPoint)
            => InteractionDetector.FindReticleTarget(CenterRay, MaxDistance, InteractableMask, _hitBuffer, out hitPoint);

        // ray 上の実コライダー → その Interactable と実ヒット点（1m 立方の手前面）が返る
        [Test]
        public void ReticleTarget_ColliderOnRay_ReturnsInteractableAndHitPoint()
        {
            var target = CreateInteractable("Target", CameraPos + new Vector3(0f, 0f, 2f));
            Physics.SyncTransforms();

            var result = Find(out var hitPoint);

            Assert.That(result, Is.SameAs(target));
            Assert.That(hitPoint.z, Is.EqualTo(CameraPos.z + 1.5f).Within(1e-3f));
        }

        // コライダーが ray を外れた位置 → 直撃対象なし（実体を外せば AABB の空気部分では直撃しない、の基礎）
        [Test]
        public void ReticleTarget_ColliderOffRay_ReturnsNull()
        {
            CreateInteractable("OffTarget", CameraPos + new Vector3(2f, 0f, 2f));
            Physics.SyncTransforms();

            var result = Find(out _);

            Assert.That(result, Is.Null);
        }

        // ray 上に 2 つ → 近い方が直撃対象（RaycastNonAlloc のヒット順に依存しない最近接選択）
        [Test]
        public void ReticleTarget_TwoCollidersOnRay_ReturnsClosest()
        {
            var near = CreateInteractable("Near", CameraPos + new Vector3(0f, 0f, 1.2f));
            CreateInteractable("Far", CameraPos + new Vector3(0f, 0f, 2.5f));
            Physics.SyncTransforms();

            var result = Find(out _);

            Assert.That(result, Is.SameAs(near));
        }

        // Interactable レイヤーだが IInteractable 非搭載のコライダー → 直撃対象なし
        [Test]
        public void ReticleTarget_ColliderWithoutInteractable_ReturnsNull()
        {
            CreatePlainCollider(CameraPos + new Vector3(0f, 0f, 2f));
            Physics.SyncTransforms();

            var result = Find(out _);

            Assert.That(result, Is.Null);
        }

        // トリガーコライダーへのヒットも直撃（拾得用に膨らませたトリガーを狙って拾える）
        [Test]
        public void ReticleTarget_TriggerCollider_ReturnsInteractable()
        {
            var target = CreateInteractable("TriggerTarget", CameraPos + new Vector3(0f, 0f, 2f), isTrigger: true);
            Physics.SyncTransforms();

            var result = Find(out _);

            Assert.That(result, Is.SameAs(target));
        }

        /// <summary>
        /// ルート（<see cref="FakeInteractable"/>）＋子コライダー（Interactable レイヤー・1m 立方）の
        /// 対象を生成する。コライダーを子に置くことで、直撃対象特定の親方向探索
        /// （GetComponentInParent）も同時に検証する。
        /// </summary>
        private FakeInteractable CreateInteractable(string name, Vector3 position, bool isTrigger = false)
        {
            var root = new GameObject(name);
            _spawned.Add(root);
            root.transform.position = position;
            var interactable = root.AddComponent<FakeInteractable>();

            var colliderGo = new GameObject(name + "Collider");
            colliderGo.transform.SetParent(root.transform, worldPositionStays: false);
            colliderGo.layer = LayerConstants.Interactable;
            var collider = colliderGo.AddComponent<BoxCollider>();
            collider.isTrigger = isTrigger;

            return interactable;
        }

        private void CreatePlainCollider(Vector3 position)
        {
            var go = new GameObject("Plain");
            _spawned.Add(go);
            go.transform.position = position;
            go.layer = LayerConstants.Interactable;
            go.AddComponent<BoxCollider>();
        }

        // GetComponentInParent の参照一致判定には階層上の実コンポーネントが必要なため、
        // モックではなく IInteractable を直接実装する最小スタブを使う
        private sealed class FakeInteractable : MonoBehaviour, IInteractable
        {
            public Vector3 CenterPosition => transform.position;
            public Bounds WorldBounds => new(transform.position, Vector3.one);
            public InteractionInputType InputType => InteractionInputType.Instant;
            public float HoldSeconds => 0f;
            public bool AllowOutOfView => false;
            public bool CanInteract() => true;
            public void Interact() { }
            public void SetInteractionState(InteractionState state, Camera viewCamera) { }
            public void SetHoldProgress(float progress01) { }
            public UniTask<bool> TryShowRejectionMessage() => UniTask.FromResult(false);
        }
    }
}
