using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Dialogs
{
    /// <summary>
    /// アイテム詳細ダイアログの 3D モデルプレビュー機構。
    /// プレハブ内 PreviewRig ルート（子に PreviewCamera / PreviewLight / ModelAnchor）にアタッチされる想定。
    /// リグは CanvasScaler の影響を受けないよう Canvas の兄弟として配置され、専用 RenderTexture 越しに
    /// <see cref="RawImage"/> へ描画する。
    /// 回転・ズーム操作は毎フレーム <see cref="Time.unscaledDeltaTime"/> 駆動（親ダイアログは Time.timeScale=0 で開くため）。
    /// </summary>
    public class HorrorItemPreviewView : MonoBehaviour
    {
        [Tooltip("プレビュー用カメラ（RenderTexture へ描画）")]
        [SerializeField] private Camera _previewCamera;

        [Tooltip("プレビュー用ライト（PreviewRig 子）")]
        [SerializeField] private Light _previewLight;

        [Tooltip("モデルの生成先（回転・ピッチはここに適用）")]
        [SerializeField] private Transform _modelAnchor;

        [Tooltip("プレビュー用 RenderTexture の一辺サイズ（px）")]
        [SerializeField] private int _textureSize = 1024;

        [Tooltip("モデルの最長辺をこのサイズへフィットさせる")]
        [SerializeField] private float _fitSize = 1f;

        [Tooltip("マウスドラッグの回転感度（度/px）")]
        [SerializeField] private float _dragSensitivity = 0.25f;

        [Tooltip("マウススクロールホイールの回転感度（度/px）")]
        [SerializeField] private float _scrollSensitivity = 0.25f;

        [Tooltip("キー/ボタン回転の速度（度/秒）")]
        [SerializeField] private float _rotateSpeed = 120f;

        [Tooltip("ピッチ角のクランプ範囲（±度）")]
        [SerializeField] private float _pitchLimit = 80f;

        [Tooltip("ズームの速度")]
        [SerializeField] private float _zoomSpeed = 15f;

        [Tooltip("ホイール1notch あたりのズーム係数変化量")]
        [SerializeField] private float _zoomStep = 0.1f;

        [Tooltip("ズーム係数の最小値（最も接近）")]
        [SerializeField] private float _zoomMin = 0.6f;

        [Tooltip("ズーム係数の最大値（最も離れる）")]
        [SerializeField] private float _zoomMax = 2f;

        private IAddressableAssetService _assetService;
        private IInputSystemService _inputService;

        private GameObject _loadedPrefab; // OnDestroy で Release するロード済みプレハブハンドル（HorrorWeaponView のイディオム）
        private RenderTexture _renderTexture;
        private Vector3 _cameraBasePosition; // ズーム前のカメラ基準ローカル座標（ズームはこの基準位置 × 係数）

        private float _yaw;
        private float _pitch;
        private float _roll;
        private float _zoom = 1f;
        private bool _initialized;

        private void Update()
        {
            if (!_initialized) return;

            var dt = Time.unscaledDeltaTime;

            if (_inputService.UI.Next.IsPressed())
                _roll -= _rotateSpeed * dt;
            else if (_inputService.UI.Previous.IsPressed())
                _roll += _rotateSpeed * dt;

            bool canInputDelta =　_inputService.UI.Click.IsPressed()
                                 || _inputService.ControlScheme != InputControlSchemes.KeyboardAndMouse;
            if (canInputDelta)
            {
                var pointerDelta = _inputService.UI.PointDelta.ReadValue<Vector2>();
                _yaw += pointerDelta.x * _dragSensitivity;
                _pitch -= pointerDelta.y * _dragSensitivity;
            }

            float scrollY = _inputService.UI.ScrollWheel.ReadValue<Vector2>().y * _scrollSensitivity;
            if (_inputService.UI.Next2.IsPressed())
                scrollY += _zoomSpeed * dt;
            else if (_inputService.UI.Previous2.IsPressed())
                scrollY -= _zoomSpeed * dt;
            _zoom = CalculateZoom(_zoom, scrollY, _zoomStep, _zoomMin, _zoomMax);

            _modelAnchor.localRotation = Quaternion.Euler(_pitch, _yaw, _roll);
            _previewCamera.transform.localPosition = _cameraBasePosition * _zoom;
        }

        private void OnDestroy()
        {
            if (_previewCamera != null) _previewCamera.targetTexture = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }

            if (_loadedPrefab != null) _assetService?.Release(_loadedPrefab);
        }

        /// <summary>
        /// プレビュー対象モデルを読み込み、専用 RenderTexture を <paramref name="output"/> へ割り当てて表示準備を行う。
        /// アイテム詳細ダイアログの Open シーケンスから、選択アイテムの ModelAssetName とともに呼ばれる想定。
        /// </summary>
        /// <param name="modelAssetName">Addressables 上のモデルプレハブアドレス。</param>
        /// <param name="output">プレビュー描画先の RawImage。</param>
        /// <param name="defaultRotationDegrees">モデルを提示する姿勢（Euler 度）。全て 0 でオーサリング姿勢のまま。</param>
        public async UniTask InitializeAsync(string modelAssetName, RawImage output, Vector3 defaultRotationDegrees)
        {
            // 描画先と同じ縦横比で生成する（正方形固定だと引き伸ばされる）
            var outputRect = output.rectTransform.rect;
            var textureWidth = CalculateTextureWidth(outputRect.width, outputRect.height, _textureSize);
            _renderTexture = new RenderTexture(textureWidth, _textureSize, 24) { antiAliasing = 4 };
            _previewCamera.targetTexture = _renderTexture;
            output.texture = _renderTexture;

            _cameraBasePosition = _previewCamera.transform.localPosition;

            _assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            _loadedPrefab = await _assetService.LoadAssetAsync<GameObject>(modelAssetName)
                .AttachExternalCancellation(destroyCancellationToken);

            var model = Object.Instantiate(_loadedPrefab, _modelAnchor);
            model.SetLayerRecursively(gameObject.layer);

            // 提示姿勢はモデル側に持たせる。回転操作は親の ModelAnchor に合成されるため、
            // リセット（操作分ゼロ）が自然にこの姿勢へ戻る
            model.transform.localRotation = Quaternion.Euler(defaultRotationDegrees);

            // ModelAssetName はドロップ品プレハブを流用するため、物理・挙動スクリプトが同梱されている可能性がある。プレビュー用に無害化する
            foreach (var col in model.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) rb.isKinematic = true;
            foreach (var behaviour in model.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (var audioSource in model.GetComponentsInChildren<AudioSource>(true))
            {
                audioSource.Stop();
                audioSource.enabled = false;
            }

            FitModel(model);

            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _initialized = true;
        }

        /// <summary>回転・ズームを初期状態（正面・等倍）へ戻す。</summary>
        public void ResetView()
        {
            _yaw = 0f;
            _pitch = 0f;
            _roll = 0f;
            _zoom = 1f;

            _modelAnchor.localRotation = Quaternion.Euler(_pitch, _yaw, _roll);
            _previewCamera.transform.localPosition = _cameraBasePosition * _zoom;
        }

        // 子孫 Renderer の bounds を合成してフィットスケールを算出・適用し、
        // スケール適用後の合成 bounds 中心が ModelAnchor 原点に一致するよう位置をオフセットする。
        private void FitModel(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            model.transform.localScale = Vector3.one * CalculateFitScale(bounds.size, _fitSize);

            var scaledBounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) scaledBounds.Encapsulate(renderers[i].bounds);

            var localCenter = _modelAnchor.InverseTransformPoint(scaledBounds.center);
            model.transform.localPosition -= localCenter;
        }

        /// <summary>
        /// 描画先の縦横比に合わせた RenderTexture の幅を算出する（高さ基準）。
        /// 描画先のサイズが未確定（0 以下）ならフォールバックとして正方形にする。
        /// </summary>
        internal static int CalculateTextureWidth(float rectWidth, float rectHeight, int textureSize)
        {
            if (rectWidth <= 0f || rectHeight <= 0f) return textureSize;
            return Mathf.Max(1, Mathf.RoundToInt(textureSize * (rectWidth / rectHeight)));
        }

        /// <summary>
        /// bounds の最長辺が targetSize に一致するフィットスケールを算出する。
        /// 最長辺が 0 以下（Renderer 無し等）ならゼロ除算を避けて等倍（1）を返す。
        /// </summary>
        internal static float CalculateFitScale(Vector3 boundsSize, float targetSize)
        {
            var longestSide = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z);
            return longestSide <= 0f ? 1f : targetSize / longestSide;
        }

        /// <summary>
        /// ホイール入力からズーム係数を算出する。scrollDelta が 0 なら現在値を維持し、
        /// それ以外は符号に応じて zoomStep 分だけ増減して min〜max にクランプする
        /// （上スクロール＝正＝拡大＝カメラ接近＝係数を減らす）。
        /// </summary>
        internal static float CalculateZoom(float current, float scrollDelta, float zoomStep, float min, float max)
        {
            if (Mathf.Approximately(scrollDelta, 0f)) return current;
            return Mathf.Clamp(current - Mathf.Sign(scrollDelta) * zoomStep, min, max);
        }
    }
}
