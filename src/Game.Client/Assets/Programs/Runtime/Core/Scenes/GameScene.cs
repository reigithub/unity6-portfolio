using System.Threading.Tasks;
using Game.Core.Constants;
using Game.Core.Extensions;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Game.Core.Scenes
{
    public abstract class GameScene
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        protected AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<GameSceneService> _sceneService;
        protected GameSceneService SceneService => _sceneService.Reference;

        protected abstract string AssetPathOrAddress { get; }

        // アセット(主にこのシーン)をロード
        protected internal virtual Task LoadAsset()
        {
            return Task.CompletedTask;
        }

        // サーバー通信など
        protected internal virtual Task PreInitialize()
        {
            return Task.CompletedTask;
        }

        // シーンビュー初期化など
        protected internal virtual Task Initialize()
        {
            return Task.CompletedTask;
        }

        // シーン起動後に演出など
        protected internal virtual Task PostInitialize()
        {
            return Task.CompletedTask;
        }

        // シーンを終了させて破棄する
        protected internal virtual Task Terminate()
        {
            return Task.CompletedTask;
        }

        // StateMachine回したくなった時(MonoBehaviour.Update)
        protected internal virtual void Update()
        {
        }
    }

    public interface IGameSceneArgs<TArgs>
    {
        public Task PreInitialize(TArgs args) => Task.CompletedTask;
    }

    public abstract class GameScene<TGameScene, TGameSceneComponent> : GameScene
        where TGameScene : GameScene<TGameScene, TGameSceneComponent>
        where TGameSceneComponent : GameSceneComponent
    {
        public TGameSceneComponent SceneComponent { get; protected set; }

        protected internal override async Task LoadAsset()
        {
            await LoadScene();
            SceneComponent = GetSceneComponent();
        }

        protected internal override Task Initialize()
        {
            return base.Initialize();
        }

        protected internal override async Task Terminate()
        {
            await UnloadScene();
        }

        protected internal override void Update()
        {
            base.Update();
        }

        protected virtual Task LoadScene()
        {
            return Task.CompletedTask;
        }

        protected virtual Task UnloadScene()
        {
            return Task.CompletedTask;
        }

        protected abstract TGameSceneComponent GetSceneComponent();
    }

    public abstract class GamePrefabScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>
        where TGameScene : GameScene<TGameScene, TGameSceneComponent>
        where TGameSceneComponent : GameSceneComponent
    {
        private GameObject _asset;
        private GameObject _instance;

        protected override async Task LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            _instance = GameObject.Instantiate(_asset);
            GameSceneHelper.MoveToGameRootScene(_instance);
        }

        protected override Task UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                _asset = null;
            }

            // var g = GameObject.Find($"{AssetPathOrAddress}" + "(Clone)");
            // g.SafeDestroy();

            return Task.CompletedTask;
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            return SceneComponent ??= GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance);
        }
    }

    public abstract class GameUnityScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>
        where TGameScene : GameScene<TGameScene, TGameSceneComponent>
        where TGameSceneComponent : GameSceneComponent
    {
        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Single;

        private SceneInstance _instance;

        protected override async Task LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
        }

        protected override async Task UnloadScene()
        {
            await AssetService.UnloadSceneAsync(_instance);
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            return SceneComponent ??= GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance.Scene);
        }
    }

    // UnityScene毎にクラス作成するはめになるので、なしの方向
    public abstract class GameUnityScene : GameScene
    {
        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Additive;

        private SceneInstance _instance;

        protected internal override async Task LoadAsset()
        {
            await LoadScene();
        }

        protected internal override async Task Terminate()
        {
            await UnloadScene();
        }

        protected virtual async Task LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
            // SceneManager.SetActiveScene(_instance.Scene);
        }

        protected virtual async Task UnloadScene()
        {
            await AssetService.UnloadSceneAsync(_instance);
        }
    }
}