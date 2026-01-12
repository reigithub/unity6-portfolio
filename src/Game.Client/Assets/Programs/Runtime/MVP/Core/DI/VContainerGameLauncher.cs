using Cysharp.Threading.Tasks;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Core.DI
{
    /// <summary>
    /// VContainerを使用したDI方式の起動（MVP用）
    /// サバイバーゲーム実装時に具体化する
    /// </summary>
    public class MVPGameLauncher : IGameModeLauncher
    {
        public GameMode Mode => GameMode.MvpSurvivor;

        private LifetimeScope _rootScope;

        public async UniTask StartupAsync()
        {
            // 1. RootLifetimeScopeを生成
            var rootObject = new GameObject("MVPRootLifetimeScope");
            Object.DontDestroyOnLoad(rootObject);

            // TODO: Survivor用のLifetimeScopeを設定
            // _rootScope = rootObject.AddComponent<SurvivorLifetimeScope>();

            // 2. コンテナ構築完了を待つ
            await UniTask.Yield();

            Debug.Log("[MVPGameLauncher] VContainer initialized (skeleton)");
        }

        public UniTask ShutdownAsync()
        {
            if (_rootScope != null)
            {
                Object.Destroy(_rootScope.gameObject);
                _rootScope = null;
            }

            return UniTask.CompletedTask;
        }
    }
}