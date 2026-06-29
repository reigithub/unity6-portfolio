using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// 親（カメラ）のワールド回転に一次遅れで追従させ、手持ちアイテムの慣性ラグを表現する。
    /// 位置は親子の剛体追従のまま、向きだけを遅延させる。カメラ回転が確定する Update の後に適用するため
    /// <see cref="LateUpdate"/> で処理する。
    /// </summary>
    public class RotationLagFollower : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Tooltip("追従の応答速度 k（1-exp(-k・dt)）。大きいほど即時、小さいほど遅れる")]
        [SerializeField] private float _followSpeed = 36f;

        // prefab の照射軸補正回転を基準として保持（カメラ正面時にライトが向くべき向き）
        private Quaternion _baseLocalRotation;

        // 一次遅れで追従する現在のワールド回転
        private Quaternion _currentRotation;

        protected void Awake()
        {
            _baseLocalRotation = transform.localRotation;
            if (_camera == null) _camera = GetComponentInParent<Camera>();
        }

        // 初期化フレームでの飛びを防ぐため目標へスナップしておく
        protected void Start() => _currentRotation = TargetRotation();

        protected void LateUpdate()
        {
            if (_camera == null) return;

            // フレームレート非依存の指数補間で目標へ追従（HorrorPlayerController の補間と統一）
            var t = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
            _currentRotation = Quaternion.Slerp(_currentRotation, TargetRotation(), t);

            // ワールド回転のみ上書き。localPosition は不変なので位置はカメラに即追従し、向きだけ遅延する
            transform.rotation = _currentRotation;
        }

        // カメラに剛体追従したときの向き（追従の目標）。_baseLocalRotation がモデル照射軸の補正。
        private Quaternion TargetRotation() => _camera.transform.rotation * _baseLocalRotation;
    }
}
