using UnityEngine;

namespace Game.Shared.LockOn
{
    /// <summary>
    /// ロックオンインジケーター（UIオーバーレイ）
    /// ターゲットのワールド座標をスクリーン座標に変換して追従
    /// 常に手前に表示され、ターゲットのサイズに関係なく一定サイズで表示
    /// </summary>
    public class LockOnIndicator : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _target;

        [Header("Settings")]
        [SerializeField] private Vector3 _worldOffset = Vector3.zero;

        [Header("Animation")]
        [SerializeField] private float _rotationSpeed = 90f;

        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseAmount = 0.1f;

        private RectTransform _rectTransform;
        private Camera _mainCamera;
        private Vector3 _baseScale;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _baseScale = transform.localScale;
        }

        private void LateUpdate()
        {
            if (_target == null || _mainCamera == null)
            {
                return;
            }

            // ターゲットが非アクティブなら非表示
            if (!_target.gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            // ワールド座標をスクリーン座標に変換
            var worldPos = _target.position + _worldOffset;
            var screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            // カメラの後ろにいる場合は非表示
            if (screenPos.z < 0)
            {
                _rectTransform.gameObject.SetActive(false);
                return;
            }

            _rectTransform.gameObject.SetActive(true);
            _rectTransform.position = screenPos;

            // 回転アニメーション
            _rectTransform.Rotate(Vector3.forward, _rotationSpeed * Time.deltaTime);

            // パルスアニメーション（スケール）
            var pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmount;
            transform.localScale = _baseScale * pulse;
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _worldOffset = new Vector3(0f, target.localScale.y / 2f, 0f);
        }

        public void SetCamera(Camera mainCamera)
        {
            _mainCamera = mainCamera;
        }
    }
}
