using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Cysharp.Threading.Tasks.Triggers;
using Game.App.Launcher;
using Game.App.Services;
using Game.App.Title;
using Game.ScoreTimeAttack;
using Game.MVP.Core.DI;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using R3;
using UnityEngine;

namespace Game.App.Bootstrap
{
    public static class GameBootstrap
    {
        private const string AppTitleAddress = "AppTitleScene";

        private static GameObject _gameBootstrap;
        private static GameModeLauncherRegistry _registry;
        private static AppSceneLoader _sceneLoader;
        private static IAppServiceProvider _appServiceProvider;
        private static bool _isInitialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // UniTask未観測例外をログ出力（Forget()で隠れる例外を検知）
            UniTaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // アプリケーションイベント購読
            ApplicationEvents.OnShutdownRequested = ShutdownAsync;
            ApplicationEvents.OnReturnToTitleRequested = ReturnToTitleAsync;

#if UNITY_EDITOR
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != AppConstants.GameRootScene)
                return;
#endif

            // アプリ脱獄チェック
            if (Application.genuineCheckAvailable && !Application.genuine)
            {
                Application.Quit(-1);
                return;
            }

            // 初期化
            InitializeAsync().Forget();
        }

        private static async UniTask InitializeAsync()
        {
            Debug.Log("[GameBootstrap] Initializing...");

            // ランチャーレジストリ初期化
            _registry = new GameModeLauncherRegistry();
            _registry.Register(new ScoreTimeAttackGameLauncher());
            _registry.Register(new SurvivorGameLauncher());

            // シーンローダー初期化
            _sceneLoader = new AppSceneLoader();

            _gameBootstrap = new GameObject("GameBootstrap");
            _gameBootstrap.GetAsyncApplicationQuitTrigger()
                .SubscribeAwait(async (_, _) => { await ShutdownAsync(); })
                .AddTo(_gameBootstrap);

            // タイトル画面表示
            await ShowTitleAsync();
        }

        private static async UniTask ShowTitleAsync()
        {
            Debug.Log("[GameBootstrap] Loading title screen...");

            // アプリサービスプロバイダーを初期化
            _appServiceProvider = new AppServiceProvider();
            await _appServiceProvider.InitializeAsync();

            var titleComponent = await _sceneLoader.LoadAsync<AppTitleSceneComponent>(AppTitleAddress);
            if (titleComponent == null)
            {
                // Debug.LogError("[GameBootstrap] Failed to load title screen. Falling back to ScoreTimeAttack.");
                DisposeAppServiceProvider();
                // await _registry.LaunchAsync(GameMode.MvcScoreTimeAttack);
                throw new NullReferenceException("[GameBootstrap] Failed to load title screen.");
                // return;
            }

            titleComponent.Initialize(_appServiceProvider);

            // ゲームモード選択を待つ
            var cts = new CancellationTokenSource();
            var selectedMode = GameMode.None;
            try
            {
                selectedMode = await titleComponent.OnGameModeSelected.FirstAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常なキャンセル（シーン遷移など）
                cts.Cancel();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameBootstrap] Game mode selection failed: {ex.Message}");
                cts.Cancel();
            }
            finally
            {
                // タイトル画面を閉じる
                _sceneLoader?.Unload();
                // アプリサービスプロバイダーを破棄（各ゲームモードで再構築）
                DisposeAppServiceProvider();
            }

            if (cts.IsCancellationRequested)
                return;

            Debug.Log($"[GameBootstrap] Selected mode: {selectedMode}");

            // 選択されたモードを起動
            await _registry.LaunchAsync(selectedMode);
        }

        private static void DisposeAppServiceProvider()
        {
            _appServiceProvider?.Dispose();
            _appServiceProvider = null;
        }

        /// <summary>
        /// タイトル画面に戻る
        /// </summary>
        public static async UniTask ReturnToTitleAsync()
        {
            Debug.Log("[GameBootstrap] Returning to title...");

            // 現在のゲームモードをシャットダウン
            await _registry.ShutdownAsync();

            // タイトル画面を表示
            await ShowTitleAsync();
        }

        public static async UniTask ShutdownAsync()
        {
            Debug.Log("[GameBootstrap] Shutting down...");

            if (_registry != null) await _registry.ShutdownAsync();
            _sceneLoader?.Unload();
            _registry = null;
            _sceneLoader = null;
            _isInitialized = false;
            _gameBootstrap.SafeDestroy();

            // グローバル例外ハンドラーを解除
            UniTaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// UniTaskの未観測例外ハンドラー
        /// Forget()で発生した例外をログ出力
        /// </summary>
        private static void OnUnobservedTaskException(Exception ex)
        {
            Debug.LogError($"[UniTask] Unobserved exception: {ex}");
        }
    }
}