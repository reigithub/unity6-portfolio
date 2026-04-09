using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Editor.Addressables;
using Game.Editor.Build;
using Game.Shared;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class GameEnvironmentSettingsWindow : EditorWindow
    {
        private Dictionary<GameEnvironment, GameEnvironmentConfig> _configs = new();
        private GameEnvironment[] _envs;
        private string[] _envNames;
        private int _index;
        private Vector2 _scrollPosition;

        [MenuItem("Window/Game Environment Settings")]
        public static void ShowWindow()
        {
            GetWindow<GameEnvironmentSettingsWindow>("ゲーム環境設定");
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("ゲーム環境設定");
            RefreshConfigs();
        }

        private void RefreshConfigs()
        {
            if (GameEnvironmentSettings.Instance?.AllConfigs == null) return;

            _configs = GameEnvironmentSettings.Instance.AllConfigs.ToDictionary(x => x.Environment);
            _envs = _configs.Keys.ToArray();
            _envNames = _envs.Select(x => x.ToString()).ToArray();
            var env = GameEnvironmentSettings.Instance.Environment;
            _index = Math.Max(0, Array.IndexOf(_envs, env));
        }

        private void OnGUI()
        {
            if (GameEnvironmentSettings.Instance == null)
            {
                EditorGUILayout.HelpBox("GameEnvironmentSettings が見つかりません", MessageType.Error);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // 環境選択
            DrawEnvironmentSelector();

            EditorGUILayout.Space(10);

            // 環境設定表示（読み取り専用）
            DrawEnvironmentConfigSection();

            EditorGUILayout.Space(10);

            // Addressables設定表示（読み取り専用）
            DrawAddressablesConfigSection();

            EditorGUILayout.Space(10);

            // 現在のAddressables状態
            DrawAddressablesCurrentStateSection();

            EditorGUILayout.Space(10);

            // Addressables ローカルバンドル同期
            DrawAddressablesSyncSection();

            EditorGUILayout.Space(10);

            // Addressables キャッシュクリア
            DrawAddressablesCacheClearSection();

            EditorGUILayout.Space(10);

            // Addressables設定適用ボタン
            DrawApplyButton();

            EditorGUILayout.EndScrollView();
        }

        private void DrawEnvironmentSelector()
        {
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            var newIndex = EditorGUILayout.Popup(_index, _envNames);
            if (_index != newIndex)
            {
                _index = newIndex;
                var newEnv = _envs[newIndex];

                // GameEnvironmentSettings を更新
                GameEnvironmentSettings.Instance.SetConfig(newEnv);
                EditorUtility.SetDirty(GameEnvironmentSettings.Instance);
                AssetDatabase.SaveAssetIfDirty(GameEnvironmentSettings.Instance);

                // Addressables Profile を自動切り替え（メモリのみ、Git差分なし）
                AddressablesEnvironmentSwitcher.SetActiveProfileFromEnvironment(newEnv, saveAsset: false);
            }
        }

        private void DrawEnvironmentConfigSection()
        {
            var config = GetCurrentConfig();
            if (config == null) return;

            EditorGUILayout.LabelField("環境設定 (読み取り専用)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.EnumPopup("DI Mode", config.DependencyResolverMode);
            EditorGUILayout.TextField("API URL", config.ApiBaseUrl);
            EditorGUILayout.TextField("gRPC URL", config.GrpcBaseUrl);
            EditorGUILayout.TextField("WebSocket URL", config.WebSocketUrl);
            EditorGUILayout.TextField("Unity Server Address", config.UnityServerAddress ?? string.Empty);
            EditorGUILayout.IntField("Unity Server Port", config.UnityServerPort);
            EditorGUILayout.TextField("Unity Session Name", config.UnityServerSessionName ?? string.Empty);
            EditorGUILayout.Toggle("Debug Log", config.EnableDebugLog);
            EditorGUILayout.Toggle("Analytics", config.EnableAnalytics);
            EditorGUILayout.Toggle("Local Master Data", config.UseLocalMasterData);
            EditorGUILayout.Toggle("Local Server Orchestrator", config.UseLocalServerOrchestrator);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawAddressablesConfigSection()
        {
            var config = GetCurrentConfig();
            var addrConfig = config?.AddressablesConfig;

            EditorGUILayout.LabelField("Addressables設定 (読み取り専用)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);

            if (addrConfig != null)
            {
                EditorGUILayout.TextField("Profile", addrConfig.ProfileName);
                EditorGUILayout.Toggle("Use Remote", addrConfig.UseRemoteLoadPath);
                EditorGUILayout.Toggle("Build Remote Catalog", addrConfig.BuildRemoteCatalog);
            }
            else
            {
                EditorGUILayout.HelpBox("Addressables設定がありません", MessageType.Warning);
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawAddressablesCurrentStateSection()
        {
            EditorGUILayout.LabelField("現在のAddressables状態", EditorStyles.boldLabel);

            var state = AddressablesEnvironmentSwitcher.GetCurrentState();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Build Target", EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.TextField("Active Profile", state.ActiveProfileName);
            EditorGUILayout.Toggle("Build Remote Catalog", state.BuildRemoteCatalog);
            EditorGUILayout.TextField("Play Mode Script", state.PlayModeScript);
            EditorGUILayout.TextField("Content.BuildPath", state.ContentBuildPath ?? "-");
            EditorGUILayout.TextField("Content.LoadPath", state.ContentLoadPath ?? "-");
            EditorGUILayout.TextField("Content Groups", $"{state.ContentGroups} / {state.TotalGroups}");
            EditorGUI.EndDisabledGroup();

            // 設定と状態の不一致チェック
            var config = GetCurrentConfig()?.AddressablesConfig;
            if (config != null && !IsConfigMatchingState(config, state))
            {
                EditorGUILayout.HelpBox("設定と状態が一致していません", MessageType.Warning);
            }
        }

        private void DrawAddressablesSyncSection()
        {
            var env = _envs != null && _index >= 0 && _index < _envs.Length ? _envs[_index] : GameEnvironment.Local;
            var state = AddressablesEnvironmentSwitcher.GetCurrentState();
            var isUseExistingBuild = state.PlayModeScript == "Use Existing Build";
            var canSync = env != GameEnvironment.Local && isUseExistingBuild;

            EditorGUILayout.LabelField("Addressables カタログ・ローカルバンドルDL", EditorStyles.boldLabel);

            if (!canSync)
            {
                var reason = env == GameEnvironment.Local
                    ? "Local環境では同期不要です"
                    : "Play Mode Script を「Use Existing Build」に設定してください";
                EditorGUILayout.HelpBox(reason, MessageType.Info);
            }

            using (new EditorGUI.DisabledGroupScope(!canSync))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("バージョン確認", GUILayout.Height(25)))
                    {
                        OnCheckRemoteVersionClicked();
                    }

                    if (GUILayout.Button("ダウンロード", GUILayout.Height(25)))
                    {
                        OnSyncNowClicked();
                    }
                }
            }

            if (canSync)
            {
                EditorGUILayout.HelpBox(
                    "UseExistingBuild モードでは、Play開始時に自動的に同期されます。\n" +
                    "手動で同期する場合は「ダウンロード」をクリックしてください。",
                    MessageType.Info);
            }
        }

        private void OnCheckRemoteVersionClicked()
        {
            EditorAddressablesSync.CheckRemoteVersionMenu();
        }

        private void OnSyncNowClicked()
        {
            EditorAddressablesSync.SyncFromRemoteMenu();
        }

        private void DrawAddressablesCacheClearSection()
        {
            EditorGUILayout.LabelField("Addressables カタログ・アセットキャッシュクリア", EditorStyles.boldLabel);

            var libraryPath = Path.Combine(Application.dataPath, "..", "Library", "com.unity.addressables");
            var catalogCachePath = Path.Combine(Application.persistentDataPath, "com.unity.addressables");
            var env = _envs != null && _index >= 0 && _index < _envs.Length ? _envs[_index] : GameEnvironment.Local;
            var downloadedAssetsPath = Path.Combine(Application.persistentDataPath, env.ToString(), "DownloadedAssets");

            var libraryExists = Directory.Exists(libraryPath);
            var catalogCacheExists = Directory.Exists(catalogCachePath);
            var downloadedAssetsExists = Directory.Exists(downloadedAssetsPath);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Library Cache", (libraryExists ? "存在" : "なし") + ": " + libraryPath);
            EditorGUILayout.TextField("Catalog Cache", (catalogCacheExists ? "存在" : "なし") + ": " + catalogCachePath);
            EditorGUILayout.TextField($"Downloaded Assets ({env})", (downloadedAssetsExists ? "存在" : "なし") + ": " + downloadedAssetsPath);
            EditorGUI.EndDisabledGroup();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledGroupScope(!libraryExists))
                {
                    if (GUILayout.Button("Library", GUILayout.Height(25)))
                    {
                        OnClearLibraryCacheClicked(libraryPath);
                    }
                }

                using (new EditorGUI.DisabledGroupScope(!catalogCacheExists))
                {
                    if (GUILayout.Button("Catalog", GUILayout.Height(25)))
                    {
                        OnClearCatalogCacheClicked(catalogCachePath);
                    }
                }

                using (new EditorGUI.DisabledGroupScope(!downloadedAssetsExists))
                {
                    if (GUILayout.Button("Downloaded", GUILayout.Height(25)))
                    {
                        OnClearDownloadedAssetsClicked(downloadedAssetsPath, env);
                    }
                }
            }

            using (new EditorGUI.DisabledGroupScope(!libraryExists && !catalogCacheExists && !downloadedAssetsExists))
            {
                if (GUILayout.Button("すべてのキャッシュをクリア", GUILayout.Height(25)))
                {
                    OnClearAllCacheClicked(libraryPath, catalogCachePath, downloadedAssetsPath, env);
                }
            }

            EditorGUILayout.HelpBox(
                "Library: エディタのAddressablesビルドキャッシュ\n" +
                "Catalog: ダウンロードしたリモートカタログのキャッシュ\n" +
                "Downloaded: ダウンロード済みリモートアセット\n\n" +
                "カタログとバンドルの不整合が発生した場合にクリアしてください。",
                MessageType.Info);
        }

        private void OnClearLibraryCacheClicked(string path)
        {
            if (!EditorUtility.DisplayDialog("確認", "Library/com.unity.addressables を削除しますか？", "削除", "キャンセル"))
                return;

            try
            {
                Directory.Delete(path, true);
                Debug.Log($"[GameEnvironmentSettings] Deleted: {path}");
                EditorUtility.DisplayDialog("完了", "Library Cache をクリアしました", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameEnvironmentSettings] Failed to delete {path}: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"削除に失敗しました: {e.Message}", "OK");
            }

            Repaint();
        }

        private void OnClearCatalogCacheClicked(string path)
        {
            if (!EditorUtility.DisplayDialog("確認", "Catalog Cache (persistentDataPath/com.unity.addressables) を削除しますか？", "削除", "キャンセル"))
                return;

            try
            {
                Directory.Delete(path, true);
                Debug.Log($"[GameEnvironmentSettings] Deleted: {path}");
                EditorUtility.DisplayDialog("完了", "Catalog Cache をクリアしました", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameEnvironmentSettings] Failed to delete {path}: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"削除に失敗しました: {e.Message}", "OK");
            }

            Repaint();
        }

        private void OnClearDownloadedAssetsClicked(string path, GameEnvironment env)
        {
            if (!EditorUtility.DisplayDialog("確認", $"Downloaded Assets ({env}) を削除しますか？\n\n{path}", "削除", "キャンセル"))
                return;

            try
            {
                Directory.Delete(path, true);
                Debug.Log($"[GameEnvironmentSettings] Deleted: {path}");
                EditorUtility.DisplayDialog("完了", $"Downloaded Assets ({env}) をクリアしました", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameEnvironmentSettings] Failed to delete {path}: {e.Message}");
                EditorUtility.DisplayDialog("エラー", $"削除に失敗しました: {e.Message}", "OK");
            }

            Repaint();
        }

        private void OnClearAllCacheClicked(string libraryPath, string catalogCachePath, string downloadedAssetsPath, GameEnvironment env)
        {
            if (!EditorUtility.DisplayDialog("確認", $"すべてのAddressablesキャッシュを削除しますか？\n\n・Library Cache\n・Catalog Cache\n・Downloaded Assets ({env})", "削除", "キャンセル"))
                return;

            var errors = new List<string>();

            if (Directory.Exists(libraryPath))
            {
                try
                {
                    Directory.Delete(libraryPath, true);
                    Debug.Log($"[GameEnvironmentSettings] Deleted: {libraryPath}");
                }
                catch (Exception e)
                {
                    errors.Add($"Library: {e.Message}");
                }
            }

            if (Directory.Exists(catalogCachePath))
            {
                try
                {
                    Directory.Delete(catalogCachePath, true);
                    Debug.Log($"[GameEnvironmentSettings] Deleted: {catalogCachePath}");
                }
                catch (Exception e)
                {
                    errors.Add($"Catalog: {e.Message}");
                }
            }

            if (Directory.Exists(downloadedAssetsPath))
            {
                try
                {
                    Directory.Delete(downloadedAssetsPath, true);
                    Debug.Log($"[GameEnvironmentSettings] Deleted: {downloadedAssetsPath}");
                }
                catch (Exception e)
                {
                    errors.Add($"Downloaded: {e.Message}");
                }
            }

            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("エラー", $"一部の削除に失敗しました:\n{string.Join("\n", errors)}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("完了", "すべてのキャッシュをクリアしました", "OK");
            }

            Repaint();
        }

        private void DrawApplyButton()
        {
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Profile設定を保存", GUILayout.Width(200), GUILayout.Height(30)))
                {
                    OnApplyButtonClicked();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.HelpBox(
                "環境変更時: Profile をメモリ上で切替 (Git差分なし)\n" +
                "保存ボタン: Profile 設定を .asset に保存 (Git差分あり)",
                MessageType.Info);
        }

        private void OnApplyButtonClicked()
        {
            var config = GetCurrentConfig()?.AddressablesConfig;
            if (config == null)
            {
                EditorUtility.DisplayDialog("エラー", "Addressables設定がありません", "OK");
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(
                "確認",
                $"Profile '{config.ProfileName}' を保存します。\n\n※ Git差分が発生します",
                "保存",
                "キャンセル");

            if (confirmed)
            {
                AddressablesEnvironmentSwitcher.SetActiveProfileOnly(config.ProfileName, saveAsset: true);
                Repaint();
                EditorUtility.DisplayDialog("完了", $"Profile '{config.ProfileName}' を保存しました", "OK");
            }
        }

        private GameEnvironmentConfig GetCurrentConfig()
        {
            if (_envs == null || _index < 0 || _index >= _envs.Length) return null;
            return _configs.TryGetValue(_envs[_index], out var config) ? config : null;
        }

        private bool IsConfigMatchingState(AddressablesEnvironmentConfig config, AddressablesCurrentState state)
        {
            // Profile 名が一致していれば OK（カスタム Path Pair 使用のため Group 切り替えは不要）
            return config.ProfileName == state.ActiveProfileName;
        }
    }
}
