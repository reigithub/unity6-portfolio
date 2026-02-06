using System;
using System.Collections.Generic;
using System.Linq;
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
                GameEnvironmentSettings.Instance.SetConfig(_envs[newIndex]);
                EditorUtility.SetDirty(GameEnvironmentSettings.Instance);
                AssetDatabase.SaveAssetIfDirty(GameEnvironmentSettings.Instance);
            }
        }

        private void DrawEnvironmentConfigSection()
        {
            var config = GetCurrentConfig();
            if (config == null) return;

            EditorGUILayout.LabelField("環境設定 (読み取り専用)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("API URL", config.ApiBaseUrl);
            EditorGUILayout.TextField("WebSocket URL", config.WebSocketUrl);
            EditorGUILayout.Toggle("Debug Log", config.EnableDebugLog);
            EditorGUILayout.Toggle("Analytics", config.EnableAnalytics);
            EditorGUILayout.Toggle("Local Master Data", config.UseLocalMasterData);
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
                EditorGUILayout.TextField("Remote Load Path", addrConfig.RemoteLoadPath ?? "-");
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
            EditorGUILayout.TextField("Active Profile", state.ActiveProfileName);
            EditorGUILayout.Toggle("Build Remote Catalog", state.BuildRemoteCatalog);
            EditorGUILayout.TextField("Remote Load Path", state.RemoteLoadPath ?? "-");
            EditorGUILayout.TextField("Remote Groups", $"{state.RemoteGroups} / {state.TotalGroups}");
            EditorGUI.EndDisabledGroup();

            // 設定と状態の不一致チェック
            var config = GetCurrentConfig()?.AddressablesConfig;
            if (config != null && !IsConfigMatchingState(config, state))
            {
                EditorGUILayout.HelpBox("設定と状態が一致していません", MessageType.Warning);
            }
        }

        private void DrawApplyButton()
        {
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Addressables設定を適用", GUILayout.Width(200), GUILayout.Height(30)))
                {
                    OnApplyButtonClicked();
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.HelpBox(
                "※ AddressableAssetData が変更されます\n" +
                "   Git差分に注意してください",
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
                "AddressableAssetData が編集されます。\n本当によろしいですか？\n\n※ Git差分が発生します",
                "適用",
                "キャンセル");

            if (confirmed)
            {
                AddressablesEnvironmentSwitcher.ApplyConfig(config);
                Repaint();
                EditorUtility.DisplayDialog("完了", "Addressables設定を適用しました", "OK");
            }
        }

        private GameEnvironmentConfig GetCurrentConfig()
        {
            if (_envs == null || _index < 0 || _index >= _envs.Length) return null;
            return _configs.TryGetValue(_envs[_index], out var config) ? config : null;
        }

        private bool IsConfigMatchingState(AddressablesEnvironmentConfig config, AddressablesCurrentState state)
        {
            if (config.ProfileName != state.ActiveProfileName) return false;
            if (config.BuildRemoteCatalog != state.BuildRemoteCatalog) return false;
            // Remote使用時はRemoteGroupが存在すべき
            if (config.UseRemoteLoadPath && state.RemoteGroups == 0) return false;
            if (!config.UseRemoteLoadPath && state.RemoteGroups > 0) return false;
            return true;
        }
    }
}
