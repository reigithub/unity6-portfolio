using Mirror;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// リモートプレイヤー（!isOwned）の簡易ビジュアル表示。
    /// SurvivorNetworkPlayerState の SyncVar 変更を監視し、
    /// Transform を補間更新する。
    /// Phase 8+ でキャラクターモデル読み込みに置き換え予定。
    /// </summary>
    public class SurvivorRemotePlayerView : MonoBehaviour
    {
        private SurvivorNetworkPlayerState _state;
        private Vector3 _targetPosition;
        private float _targetRotationY;
        private const float InterpSpeed = 15f;

        public void Initialize(SurvivorNetworkPlayerState state)
        {
            _state = state;
            _targetPosition = transform.position;
            _targetRotationY = transform.eulerAngles.y;
            _state.OnStateUpdated += OnStateChanged;

            // カプセルで簡易表示
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(transform);
            capsule.transform.localPosition = Vector3.up;
            capsule.transform.localScale = Vector3.one;

            // 当たり判定はサーバーで処理するため削除
            var col = capsule.GetComponent<CapsuleCollider>();
            if (col != null) Destroy(col);

            // 他プレイヤーの色を区別
            var renderer = capsule.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.8f, 0.9f); // シアン系
            }
        }

        private void OnStateChanged(
            SurvivorNetworkPlayerStateSnapshot prev,
            SurvivorNetworkPlayerStateSnapshot current)
        {
            _targetPosition = new Vector3(current.PositionX, current.PositionY, current.PositionZ);
            _targetRotationY = current.RotationY;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(
                transform.position, _targetPosition,
                Time.deltaTime * InterpSpeed);

            var rot = transform.eulerAngles;
            rot.y = Mathf.LerpAngle(rot.y, _targetRotationY, Time.deltaTime * InterpSpeed);
            transform.eulerAngles = rot;
        }

        private void OnDestroy()
        {
            if (_state != null)
                _state.OnStateUpdated -= OnStateChanged;
        }
    }
}
