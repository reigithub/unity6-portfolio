using System;
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
    /// GameServiceManagerに依存せず、VContainerのみで依存関係を解決
    /// </summary>
    public class SurvivorGameLauncher : IGameModeLauncher
    {
        public GameMode Mode => GameMode.MvpSurvivor;

        private LifetimeScope _rootScope;
        private ISurvivorGameRunner _gameRunner;

        // LifetimeScopeの型を外部から登録
        private static Type _lifetimeScopeType;

        /// <summary>
        /// LifetimeScopeの型を登録（Survivor側から呼び出す）
        /// </summary>
        public static void RegisterLifetimeScopeType<T>() where T : LifetimeScope
        {
            _lifetimeScopeType = typeof(T);
        }

        public async UniTask StartupAsync()
        {
            if (_lifetimeScopeType == null)
            {
                throw new InvalidOperationException(
                    "LifetimeScope type is not registered. Call SurvivorGameLauncher.RegisterLifetimeScopeType<T>() first.");
            }

            // 1. VContainer RootLifetimeScopeを生成
            var rootObject = new GameObject("SurvivorLifetimeScope");
            UnityEngine.Object.DontDestroyOnLoad(rootObject);
            _rootScope = (LifetimeScope)rootObject.AddComponent(_lifetimeScopeType);

            // 2. コンテナからゲームランナーを解決
            _gameRunner = _rootScope.Container.Resolve<ISurvivorGameRunner>();

            // 3. ゲーム開始
            await _gameRunner.StartupAsync();

            Debug.Log("[SurvivorGameLauncher] MVP mode initialized via VContainer");
        }

        public async UniTask ShutdownAsync()
        {
            if (_gameRunner != null)
            {
                await _gameRunner.ShutdownAsync();
                _gameRunner = null;
            }

            if (_rootScope != null)
            {
                UnityEngine.Object.Destroy(_rootScope.gameObject);
                _rootScope = null;
            }
        }
    }

    /// <summary>
    /// MVPゲームのエントリポイントインターフェース
    /// </summary>
    public interface ISurvivorGameRunner
    {
        UniTask StartupAsync();
        UniTask ShutdownAsync();
    }

    /// <summary>
    /// ゲームルートコントローラーのインターフェース
    /// </summary>
    public interface IGameRootController
    {
        UnityEngine.Camera MainCamera { get; }
        void SetFollowTarget(UnityEngine.Transform target);
        void ClearFollowTarget();
        void SetCameraRadius(Vector2 scrollWheel);
        void SetDirectionalLightActive(bool active);
        void SetSkyboxMaterial(UnityEngine.Material material);
        void ResetSkyboxMaterial();
        DG.Tweening.Tweener FadeIn(float duration = 0.5f);
        DG.Tweening.Tweener FadeOut(float duration = 0.5f);
        void SetFadeImmediate(float alpha);
    }
}