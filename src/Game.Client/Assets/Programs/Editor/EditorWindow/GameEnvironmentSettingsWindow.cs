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
