using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.MVP.Core.DI;
using Game.MVP.Core.Enums;
using Game.Shared.Extensions;
using Game.Shared.Scenes;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Core.Scenes
{
    public abstract class GameScene : IGameScene
    {
        protected abstract string AssetPathOrAddress { get; }

        public GameSceneState State { get; set; }
        public Func<IGameScene, UniTask> ArgHandler { get; set; }

        public CompositeDisposable Disposables { get; } = new();

        public virtual UniTask PreInitialize()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask LoadAsset()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Startup()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Sleep()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Restart()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Ready()
        {
            return UniTask.CompletedTask;
        }

        public virtual UniTask Terminate()
        {
            return UniTask.CompletedTask;
        }
    }

    public abstract class GameScene<TGameScene, TGameSceneComponent> : GameScene
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        protected TGameSceneComponent SceneComponent { get; set; }

        public override UniTask PreInitialize()
        {
            return base.PreInitialize();
        }

        public override async UniTask LoadAsset()
        {
            await LoadScene();
            SceneComponent = GetSceneComponent();
        }

        public override UniTask Startup()
        {
            return base.Startup();
        }

        public override UniTask Ready()
        {
            return base.Ready();
        }

        public override UniTask Sleep()
        {
            SceneComponent.Sleep();
            return base.Sleep();
        }

        public override UniTask Restart()
        {
            SceneComponent.Restart();
            return Ready();
        }

        public override async UniTask Terminate()
        {
            await UnloadScene();
            await base.Terminate();
        }

        protected virtual UniTask LoadScene()
        {
            return UniTask.CompletedTask;
        }

        protected virtual UniTask UnloadScene()
        {
            return UniTask.CompletedTask;
        }

        protected abstract TGameSceneComponent GetSceneComponent();
    }

    public abstract class GamePrefabScene<TGameScene, TGameSceneComponent> :
        GameScene<TGameScene, TGameSceneComponent>,
        IGameSceneFader
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        [Inject] protected IAddressableAssetService AssetService { get; set; }
        [Inject] protected IObjectResolver Resolver { get; set; }
        [Inject] protected IGameRootController GameRootController { get; set; }

        private GameObject _asset;
        private GameObject _instance;
        private CanvasGroup _canvasGroup;

        protected override async UniTask LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            Scene scene = GameSceneHelper.GetGameRootScene();
            _instance = scene.IsValid()
                ? UnityEngine.Object.Instantiate(_asset, new InstantiateParameters { scene = scene })
                : UnityEngine.Object.Instantiate(_asset);

            // GameObjectとその子にDIを注入
            Resolver?.InjectGameObject(_instance);

            // CanvasGroupの有無をキャッシュ（ローカルフェード判定用）
            _instance.TryGetComponent(out _canvasGroup);
        }

        protected override UniTask UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                AssetService.ReleaseAsset(_asset);
                _asset = null;
                _canvasGroup = null;
            }

            return UniTask.CompletedTask;
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            if (SceneComponent == null)
            {
                SceneComponent = GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance);
                Resolver?.Inject(SceneComponent);
            }

            return SceneComponent;
        }

        #region IGameSceneFader

        public virtual async UniTask FadeInAsync(float duration = 0.3f)
        {
            if (_canvasGroup != null)
            {
                // ローカルフェード（CanvasGroup）
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                _instance.SetActive(true);
                await _canvasGroup.DOFade(1f, duration).SetUpdate(true).ToUniTask();
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            // グローバルフェードインは必ず実行
            if (GameRootController != null)
            {
                // グローバルフェード（GameRootController）
                var tweener = GameRootController.FadeIn(duration);
                if (tweener != null)
                {
                    await tweener.ToUniTask();
                }
            }
        }

        public virtual async UniTask FadeOutAsync(float duration = 0.3f)
        {
            if (_canvasGroup != null)
            {
                // ローカルフェード（CanvasGroup）
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
                await _canvasGroup.DOFade(0f, duration).SetUpdate(true).ToUniTask();
                _instance.SetActive(false);
            }

            if (GameRootController != null)
            {
                // グローバルフェード（GameRootController）
                var tweener = GameRootController.FadeOut(duration);
                if (tweener != null)
                {
                    await tweener.ToUniTask();
                }
            }
        }

        #endregion
    }

    // コンポーネント付きのUnityScene
    // Memo: Prefabシーンと併用した際の影響をすべて検討完了するまでは使用禁止
    // GameRootScene(PrefabSceneロード) + UnityシーンAdditiveロード方式で賄えない要件が発生した際に改めて検討する
    public abstract class GameUnityScene<TGameScene, TGameSceneComponent> : GameScene<TGameScene, TGameSceneComponent>, IGameSceneFader
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        [Inject] protected IAddressableAssetService AssetService { get; set; }
        [Inject] protected IObjectResolver Resolver { get; set; }
        [Inject] protected IGameRootController GameRootController { get; set; }

        protected virtual LoadSceneMode LoadSceneMode => LoadSceneMode.Single;

        private SceneInstance _instance;

        protected override async UniTask LoadScene()
        {
            _instance = await AssetService.LoadSceneAsync(AssetPathOrAddress, LoadSceneMode, activateOnLoad: true);
        }

        protected override async UniTask UnloadScene()
        {
            await AssetService.UnloadSceneAsync(_instance);
            // Memo: どのタイミングでGameRootSceneを戻すか要検討 -> プレハブシーンに遷移する前にチェックするのが妥当?
            // 1. プレハブ -> Unityシーン : 戻る時にプレハブシーン -> 事前に戻す必要あり
            // 2. Unityシーン -> プレハブ : 遷移時にプレハブシーン -> 事前に戻す必要あり
            // await SceneManager.LoadSceneAsync(AppConstants.GameRootScene, LoadSceneMode.Single);
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            if (SceneComponent == null)
            {
                SceneComponent = GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance.Scene);
                Resolver?.Inject(SceneComponent);
            }

            return SceneComponent;
        }

        public async UniTask FadeInAsync(float duration = 0.3f)
        {
            // グローバルフェードインは必ず実行
            if (GameRootController != null)
            {
                var tweener = GameRootController.FadeIn(duration);
                if (tweener != null)
                {
                    await tweener.ToUniTask();
                }
            }
        }

        public async UniTask FadeOutAsync(float duration = 0.3f)
        {
            if (GameRootController != null)
            {
                var tweener = GameRootController.FadeOut(duration);
                if (tweener != null)
                {
                    await tweener.ToUniTask();
                }
            }
        }
    }

    /// <summary>
    /// ダイアログ用のGameScene基底クラス
    /// </summary>
    public abstract class GameDialogScene<TGameScene, TGameSceneComponent, TResult> :
        GameScene<TGameScene, TGameSceneComponent>,
        IGameSceneResult<TResult>
        where TGameScene : IGameScene
        where TGameSceneComponent : IGameSceneComponent
    {
        [Inject] protected IAddressableAssetService AssetService { get; set; }
        [Inject] protected IObjectResolver Resolver { get; set; }

        public UniTaskCompletionSource<TResult> ResultTcs { get; set; }

        private GameObject _asset;
        private GameObject _instance;

        protected override async UniTask LoadScene()
        {
            _asset = await AssetService.LoadAssetAsync<GameObject>(AssetPathOrAddress);
            Scene scene = GameSceneHelper.GetGameRootScene();
            _instance = scene.IsValid()
                ? UnityEngine.Object.Instantiate(_asset, new InstantiateParameters { scene = scene })
                : UnityEngine.Object.Instantiate(_asset);

            // GameObjectとその子にDIを注入
            Resolver?.InjectGameObject(_instance);
        }

        protected override UniTask UnloadScene()
        {
            if (_instance)
            {
                _instance.SafeDestroy();
                _instance = null;
                AssetService.ReleaseAsset(_asset);
                _asset = null;
            }

            return UniTask.CompletedTask;
        }

        protected override TGameSceneComponent GetSceneComponent()
        {
            if (SceneComponent == null)
            {
                SceneComponent = GameSceneHelper.GetSceneComponent<TGameSceneComponent>(_instance);
                Resolver?.Inject(SceneComponent);
            }

            return SceneComponent;
        }

        /// <summary>
        /// 結果をセットしてダイアログを閉じる
        /// </summary>
        public bool TrySetResult(TResult result) => ResultTcs?.TrySetResult(result) ?? false;

        /// <summary>
        /// キャンセルしてダイアログを閉じる
        /// </summary>
        public bool TrySetCanceled() => ResultTcs?.TrySetCanceled() ?? false;
    }
}
