using Cysharp.Threading.Tasks;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Core.Services;
using Game.MVP.Survivor.Root;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes;
using Game.Shared.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Survivor
{
    /// <summary>
    /// Survivorゲームのエントリポイント
    /// VContainerから依存性を注入され、ゲームの起動・終了を管理
    /// MVC依存なし、純粋なMVPシステムのみを使用
    /// </summary>
    public class SurvivorGameRunner : ISurvivorGameRunner
    {
        private readonly IObjectResolver _container;
        private readonly IGameSceneService _sceneService;
        private readonly IAddressableAssetService _assetService;
        private readonly IMasterDataService _masterDataService;
        private readonly IAudioService _audioService;
        private readonly IInputService _inputService;
        private readonly ISurvivorSaveService _saveService;
        private readonly IPersistentObjectProvider _persistentObjectProvider;

        private GameObject _gameRootInstance;
        private SurvivorGameRootController _gameRootController;

        public SurvivorGameRunner(
            IObjectResolver container,
            IGameSceneService sceneService,
            IAddressableAssetService assetService,
            IMasterDataService masterDataService,
            IAudioService audioService,
            IInputService inputService,
            ISurvivorSaveService saveService,
            IPersistentObjectProvider persistentObjectProvider)
        {
            _container = container;
            _sceneService = sceneService;
            _assetService = assetService;
            _masterDataService = masterDataService;
            _audioService = audioService;
            _inputService = inputService;
            _saveService = saveService;
            _persistentObjectProvider = persistentObjectProvider;
        }

        public async UniTask StartupAsync()
        {
            // 1. サービス起動
            _audioService.Startup();
            _inputService.Startup();

            // 2. マスターデータ読み込み
            await _masterDataService.LoadMasterDataAsync();

            // 3. セーブデータ読み込み
            await _saveService.LoadAsync();

            // 4. 共通オブジェクト読み込み（カメラ、UIルートなど）
            await LoadGameRootControllerAsync();

            // 5. 初期シーンへ遷移
            await _sceneService.TransitionAsync<SurvivorTitleScene>();

            Debug.Log("[SurvivorGameRunner] Game started");
        }

        private async UniTask LoadGameRootControllerAsync()
        {
            var prefab = await _assetService.LoadAssetAsync<GameObject>("SurvivorGameRootController");
            if (prefab == null)
            {
                Debug.LogError("[SurvivorGameRunner] Failed to load SurvivorGameRootController prefab");
                return;
            }

            _gameRootInstance = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(_gameRootInstance);

            // VContainerで依存性を注入
            _container.InjectGameObject(_gameRootInstance);

            // コントローラーを取得して初期化
            _gameRootController = _gameRootInstance.GetComponent<SurvivorGameRootController>();
            if (_gameRootController != null)
            {
                _gameRootController.Initialize();

                // 永続オブジェクトとして登録
                _persistentObjectProvider.Register<IGameRootController>(_gameRootController);
            }
            else
            {
                Debug.LogError("[SurvivorGameRunner] SurvivorGameRootController component not found");
            }
        }

        public async UniTask ShutdownAsync()
        {
            // セーブデータ保存（変更がある場合のみ）
            await _saveService.SaveIfDirtyAsync();

            // 全てのシーンを終了させる
            await _sceneService.TerminateAllAsync();

            // サービスシャットダウン
            _audioService.Shutdown();
            _inputService.Shutdown();

            // 永続オブジェクトの登録解除
            _persistentObjectProvider.Clear();

            // 共通オブジェクト破棄
            if (_gameRootInstance != null)
            {
                Object.Destroy(_gameRootInstance);
                _gameRootInstance = null;
            }

            await UniTask.Yield();
            Debug.Log("[SurvivorGameRunner] Game shutdown");
        }
    }
}