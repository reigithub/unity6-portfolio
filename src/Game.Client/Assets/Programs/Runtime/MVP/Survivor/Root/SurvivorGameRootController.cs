using DG.Tweening;
using MessagePipe;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using Game.MVP.Core.DI;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Signals;
using Game.Shared.Services;

namespace Game.MVP.Survivor.Root
{
    /// <summary>
    /// Survivorゲーム全体で共有するオブジェクトを管理
    /// DontDestroyOnLoadで永続化される
    /// </summary>
    public class SurvivorGameRootController : MonoBehaviour, IGameRootController
    {
        [Header("Camera")]
        [SerializeField] private Camera _mainCamera;

        [SerializeField] private SurvivorPlayerFollowCameraController _playerFollowCamera;

        [Header("Lighting")]
        [SerializeField] private GameObject _directionalLight;

        [SerializeField] private Skybox _skybox;

        [Header("UI")]
        [SerializeField] private Canvas _fadeCanvas;

        [SerializeField] private Image _fadeImage;

        // VContainer Injection
        [Inject] private IInputService _inputService;
        [Inject] private ISubscriber<SurvivorSignals.Player.Spawned> _playerSpawnedSubscriber;

        private Material _defaultSkyboxMaterial;
        private readonly CompositeDisposable _disposables = new();

        /// <summary>
        /// メインカメラ
        /// </summary>
        public Camera MainCamera => _mainCamera;

        /// <summary>
        /// 初期化
        /// </summary>
        public void Initialize()
        {
            if (_playerFollowCamera != null)
                _playerFollowCamera.Initialize();

            // 初期状態設定
            if (_fadeImage != null)
            {
                _fadeImage.color = new Color(_fadeImage.color.r, _fadeImage.color.g, _fadeImage.color.b, 1f);
            }

            if (_skybox != null)
            {
                _defaultSkyboxMaterial = _skybox.material;
            }

            // シグナル購読
            SubscribeSignals();
        }

        private void SubscribeSignals()
        {
            // プレイヤースポーン時にカメラのフォロー対象を設定
            _playerSpawnedSubscriber?
                .Subscribe(signal => SetFollowTarget(signal.PlayerTransform))
                .AddTo(_disposables);
        }

        /// <summary>
        /// カメラのフォロー対象を設定
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            if (_playerFollowCamera != null && target != null)
                _playerFollowCamera.SetFollowTarget(target);
        }

        /// <summary>
        /// フォロー対象をクリア
        /// </summary>
        public void ClearFollowTarget()
        {
            if (_playerFollowCamera != null)
                _playerFollowCamera.ClearFollowTarget();
        }

        public void SetCameraRadius(Vector2 scrollWheel)
        {
            if (_playerFollowCamera != null)
                _playerFollowCamera.SetCameraRadius(scrollWheel);
        }

        /// <summary>
        /// DirectionalLightの有効/無効を切り替え
        /// </summary>
        public void SetDirectionalLightActive(bool active)
        {
            if (_directionalLight != null)
                _directionalLight.SetActive(active);
        }

        /// <summary>
        /// Skyboxマテリアルを設定
        /// </summary>
        public void SetSkyboxMaterial(Material material)
        {
            if (_skybox != null)
                _skybox.material = material;
        }

        /// <summary>
        /// デフォルトのSkyboxに戻す
        /// </summary>
        public void ResetSkyboxMaterial()
        {
            if (_skybox != null && _defaultSkyboxMaterial != null)
                _skybox.material = _defaultSkyboxMaterial;
        }

        /// <summary>
        /// フェードイン（Time.timeScale=0でも動作）
        /// </summary>
        public Tweener FadeIn(float duration = 0.5f)
        {
            if (_fadeImage == null) return null;
            return _fadeImage.DOFade(0f, duration).SetUpdate(true);
        }

        /// <summary>
        /// フェードアウト（Time.timeScale=0でも動作）
        /// </summary>
        public Tweener FadeOut(float duration = 0.5f)
        {
            if (_fadeImage == null) return null;
            return _fadeImage.DOFade(1f, duration).SetUpdate(true);
        }

        /// <summary>
        /// 即座にフェード状態を設定
        /// </summary>
        public void SetFadeImmediate(float alpha)
        {
            if (_fadeImage != null)
            {
                var color = _fadeImage.color;
                color.a = alpha;
                _fadeImage.color = color;
            }
        }

        private bool _rightClick;

        private void Update()
        {
            _rightClick = _inputService.UI.RightClick.IsPressed();
        }

        private void LateUpdate()
        {
            // Memo: CinemachineDefaultInputSystemへの干渉の仕方について再考の余地あり
            if (_playerFollowCamera != null)
                _playerFollowCamera.SetInputAxisEnable(_rightClick);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}