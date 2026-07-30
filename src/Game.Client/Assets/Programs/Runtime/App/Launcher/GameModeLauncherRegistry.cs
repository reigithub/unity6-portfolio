using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.App.Launcher
{
    /// <summary>
    /// ゲームモードランチャーの管理
    /// </summary>
    public class GameModeLauncherRegistry
    {
        private readonly Dictionary<GameMode, IGameModeLauncher> _launchers = new();
        private IGameModeLauncher _currentLauncher;

        public GameMode CurrentMode => _currentLauncher?.Mode ?? GameMode.None;

        public void Register(IGameModeLauncher launcher)
        {
            _launchers[launcher.Mode] = launcher;
            Debug.Log($"[GameModeLauncherRegistry] Registered: {launcher.Mode}");
        }

        public async UniTask LaunchAsync(GameMode mode)
        {
            // 現在のモードをシャットダウン
            if (_currentLauncher != null)
            {
                Debug.Log($"[GameModeLauncherRegistry] Shutting down: {_currentLauncher.Mode}");
                await _currentLauncher.ShutdownAsync();
                _currentLauncher = null;
            }

            // 新しいモードを起動
            if (_launchers.TryGetValue(mode, out var launcher))
            {
                Debug.Log($"[GameModeLauncherRegistry] Launching: {mode}");
                _currentLauncher = launcher;
                try
                {
                    await launcher.StartupAsync();
                }
                catch
                {
                    // 起動途中で失敗したランチャーを Shutdown 対象に残すと、未登録サービスの解決で二次例外になるため外す
                    _currentLauncher = null;
                    throw;
                }
            }
            else
            {
                Debug.LogError($"[GameModeLauncherRegistry] Launcher not found: {mode}");
            }
        }

        public async UniTask ShutdownAsync()
        {
            if (_currentLauncher != null)
            {
                Debug.Log($"[GameModeLauncherRegistry] Shutting down: {_currentLauncher.Mode}");
                await _currentLauncher.ShutdownAsync();
                _currentLauncher = null;
            }
        }
    }
}