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
    /// <see cref="InteractionDetector.IsOccluded"/> の遮蔽判定検証。
    /// 特に「Interactable 化された家具（ソファ等）が他の Interactable の遮蔽物として機能せず、
    /// 背後のアイテムのプロンプトが貫通表示された不具合」の回帰を防止する。
    /// 実コライダーへの Raycast を使うため、生成後に <see cref="Physics.SyncTransforms"/> で
    /// ブロードフェーズを同期する（Raycast はシミュレーション不要）。エディタで開いている
    /// シーンのコライダーとの干渉を避けるため、テストリグは遠隔座標に配置する。
    /// </summary>
    [TestFixture]
    public class InteractionDetectorOcclusionTests
    {
        // 開いているシーンのレベルジオメトリと交差しない遠隔座標をレイの始点（カメラ相当）とする
        private static readonly Vector3 CameraPos = new(5000f, 5000f, 5000f);

        // 対象（遮蔽判定のレイ終点）。レイが対象自身の前面（z=+4.5）を必ず貫く距離に置く
        private static readonly Vector3 TargetPos = CameraPos + new Vector3(0f, 0f, 5f);

        // カメラと対象の中間の遮蔽物位置
        private static readonly Vector3 BetweenPos = CameraPos + new Vector3(0f, 0f, 2.5f);

        // HorrorPlayer.prefab の設定相当（_occluderMask | _interactableMask）を定数で再現
        private static readonly int OcclusionMask =
            LayerMaskConstants.Default | LayerMaskConstants.Ground |
            LayerMaskConstants.Structure | LayerMaskConstants.Interactable;

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

        // 間に Structure レイヤーの壁 → 遮蔽（従来動作の回帰確認）
        [Test]
        public void IsOccluded_StructureWallBetween_ReturnsTrue()
        {
            var target = CreateInteractable("Target", TargetPos);
            CreateWall(BetweenPos, LayerConstants.Structure);
            Physics.SyncTransforms();

            bool occluded = InteractionDetector.IsOccluded(CameraPos, TargetPos, target, OcclusionMask, _hitBuffer);

            Assert.IsTrue(occluded, "Structure レイヤーの壁が遮蔽物として扱われていません");
        }

        // 間に別 Interactable のソリッドコライダー → 遮蔽（プロンプト貫通バグの回帰防止の本命）
        [Test]
        public void IsOccluded_OtherInteractableBetween_ReturnsTrue()
        {
            var target = CreateInteractable("Target", TargetPos);
            CreateInteractable("Sofa", BetweenPos);
            Physics.SyncTransforms();

            bool occluded = InteractionDetector.IsOccluded(CameraPos, TargetPos, target, OcclusionMask, _hitBuffer);

            Assert.IsTrue(occluded, "Interactable レイヤーの他対象が遮蔽物として扱われていません（プロンプト貫通バグの回帰）");
        }

        // 対象自身のコライダーのみ → 非遮蔽（自己ヒットの参照一致除外）
        [Test]
        public void IsOccluded_OnlyTargetOwnCollider_ReturnsFalse()
        {
            var target = CreateInteractable("Target", TargetPos);
            Physics.SyncTransforms();

            bool occluded = InteractionDetector.IsOccluded(CameraPos, TargetPos, target, OcclusionMask, _hitBuffer);

            Assert.IsFalse(occluded, "対象自身のコライダーが自己遮蔽になっています（自分のプロンプトが消える退行）");
        }

        // 間にトリガーコライダー → 非遮蔽（拾得用に膨らませたトリガーは遮蔽物にしない）
        [Test]
        public void IsOccluded_TriggerColliderBetween_ReturnsFalse()
        {
            var target = CreateInteractable("Target", TargetPos);
            CreateWall(BetweenPos, LayerConstants.Structure, isTrigger: true);
            Physics.SyncTransforms();

            bool occluded = InteractionDetector.IsOccluded(CameraPos, TargetPos, target, OcclusionMask, _hitBuffer);

            Assert.IsFalse(occluded, "トリガーコライダーが遮蔽物として扱われています");
        }

        /// <summary>
        /// ルート（<see cref="FakeInteractable"/>）＋子コライダー（Interactable レイヤー・1m 立方）の
        /// 対象を生成する。コライダーを子に置くことで、自己ヒット除外の親方向探索
        /// （GetComponentInParent）も同時に検証する。
        /// </summary>
        private FakeInteractable CreateInteractable(string name, Vector3 position)
        {
            var root = new GameObject(name);
            _spawned.Add(root);
            root.transform.position = position;
            var interactable = root.AddComponent<FakeInteractable>();

            var colliderGo = new GameObject(name + "Collider");
            colliderGo.transform.SetParent(root.transform, worldPositionStays: false);
            colliderGo.layer = LayerConstants.Interactable;
            colliderGo.AddComponent<BoxCollider>();

            return interactable;
        }

        private void CreateWall(Vector3 position, int layer, bool isTrigger = false)
        {
            var wall = new GameObject("Wall");
            _spawned.Add(wall);
            wall.transform.position = position;
            wall.layer = layer;
            var collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, 3f, 0.2f);
            collider.isTrigger = isTrigger;
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
