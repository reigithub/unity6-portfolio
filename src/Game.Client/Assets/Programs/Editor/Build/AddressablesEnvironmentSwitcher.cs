using System;
using System.Linq;
using Game.Shared;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Game.Editor.Build
{
    /// <summary>
    /// GameEnvironment に基づいて Addressables 設定を切り替える
    /// </summary>
    public static class AddressablesEnvironmentSwitcher
    {
        /// <summary>
        /// Local固定にするGroup名のパターン
        /// </summary>
        private static readonly string[] LocalOnlyPatterns =
        {
            "Default",
            "Local",
            "Develop",
            "Built-in",
            "BuiltIn"
        };

        /// <summary>
        /// 設定を適用（確認ダイアログなし - 内部用）
        /// </summary>
        [Obsolete("Remote.BuildPath/LoadPathはRemote.BuildPath/LoadPathに統合されました。ApplyFromEnvironmentVariableを使用してください。")]
        public static void ApplyConfig(AddressablesEnvironmentConfig config)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] AddressableAssetSettings が見つかりません");
                return;
            }

            Debug.Log($"[Addressables] 設定適用開始: Profile={config.ProfileName}, UseRemote={config.UseRemoteLoadPath}");

            // 1. Profile 切替（Remote.LoadPath は Profile から自動取得される）
            SetActiveProfile(settings, config.ProfileName);

            // 2. BuildRemoteCatalog 設定
            settings.BuildRemoteCatalog = config.BuildRemoteCatalog;

            // 3. 全GroupのBuild/Load Path を切替
            SetAllGroupsBuildMode(settings, config.UseRemoteLoadPath);

            // ログ出力: Profile の Remote パスを表示
            var remoteBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath");
            var remoteLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath");
            Debug.Log($"[Addressables] Remote.BuildPath: {remoteBuildPath}");
            Debug.Log($"[Addressables] Remote.LoadPath: {remoteLoadPath}");

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log("[Addressables] 設定適用完了");
        }

        /// <summary>
        /// GameEnvironment から設定を適用
        /// </summary>
        [Obsolete("Remote.BuildPath/LoadPathはRemote.BuildPath/LoadPathに統合されました。ApplyFromEnvironmentVariableを使用してください。")]
        public static void ApplyFromEnvironment(GameEnvironment environment)
        {
            var envSettings = GameEnvironmentSettings.Instance;
            if (envSettings == null)
            {
                Debug.LogError("[Addressables] GameEnvironmentSettings が見つかりません");
                return;
            }

            var config = envSettings.AllConfigs.FirstOrDefault(c => c.Environment == environment);
            if (config?.AddressablesConfig == null)
            {
                Debug.LogError($"[Addressables] 環境 {environment} の設定が見つかりません");
                return;
            }

            ApplyConfig(config.AddressablesConfig);
        }

        /// <summary>
        /// CI環境から環境変数で Profile を切り替え
        /// 環境変数: GAME_ENVIRONMENT
        /// カスタム Path Pair 方式では Profile 切り替えのみで Local/Remote が切り替わる
        /// </summary>
        public static void ApplyFromEnvironmentVariable()
        {
            var envName = Environment.GetEnvironmentVariable("GAME_ENVIRONMENT");
            if (string.IsNullOrEmpty(envName))
            {
                Debug.Log("[Addressables] GAME_ENVIRONMENT 環境変数が設定されていません。スキップします。");
                return;
            }

            if (Enum.TryParse<GameEnvironment>(envName, true, out var env))
            {
                Debug.Log($"[Addressables] 環境変数から Profile 切り替え: {env}");
                // メモリ上のみ切り替え（CI/CD ではビルド後にプロセス終了するため保存不要）
                SetActiveProfileFromEnvironment(env, saveAsset: false);
            }
            else
            {
                Debug.LogError($"[Addressables] 無効な環境名: {envName}");
            }
        }

        /// <summary>
        /// アクティブプロファイルを設定
        /// </summary>
        private static void SetActiveProfile(AddressableAssetSettings settings, string profileName)
        {
            var profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                Debug.LogWarning($"[Addressables] Profile '{profileName}' が見つかりません。現在のプロファイルを維持します。");
                return;
            }

            settings.activeProfileId = profileId;
            Debug.Log($"[Addressables] Profile 切替: {profileName}");
        }

        /// <summary>
        /// Profile のみを切り替える（Git差分を最小化）
        /// </summary>
        /// <param name="profileName">Profile 名</param>
        /// <param name="saveAsset">true: .asset に保存（Git差分発生）, false: メモリのみ（Git差分なし）</param>
        /// <returns>切り替え成功時 true</returns>
        public static bool SetActiveProfileOnly(string profileName, bool saveAsset = false)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] AddressableAssetSettings が見つかりません");
                return false;
            }

            var profileId = settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                Debug.LogWarning($"[Addressables] Profile '{profileName}' が見つかりません");
                return false;
            }

            var previousProfile = settings.profileSettings.GetProfileName(settings.activeProfileId);
            settings.activeProfileId = profileId;

            if (saveAsset)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Addressables] Profile 切替 (保存): {previousProfile} → {profileName}");
            }
            else
            {
                Debug.Log($"[Addressables] Profile 切替 (メモリのみ): {previousProfile} → {profileName}");
            }

            return true;
        }

        /// <summary>
        /// GameEnvironment から Profile のみを切り替える（Git差分を最小化）
        /// </summary>
        /// <param name="environment">対象環境</param>
        /// <param name="saveAsset">true: .asset に保存（Git差分発生）, false: メモリのみ（Git差分なし）</param>
        /// <returns>切り替え成功時 true</returns>
        public static bool SetActiveProfileFromEnvironment(GameEnvironment environment, bool saveAsset = false)
        {
            var envSettings = GameEnvironmentSettings.Instance;
            if (envSettings == null)
            {
                Debug.LogError("[Addressables] GameEnvironmentSettings が見つかりません");
                return false;
            }

            var config = envSettings.AllConfigs.FirstOrDefault(c => c.Environment == environment);
            if (config?.AddressablesConfig == null)
            {
                Debug.LogWarning($"[Addressables] 環境 {environment} の Addressables 設定がありません");
                return false;
            }

            return SetActiveProfileOnly(config.AddressablesConfig.ProfileName, saveAsset);
        }

        /// <summary>
        /// 現在の Profile 名を取得
        /// </summary>
        public static string GetCurrentProfileName()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings?.profileSettings.GetProfileName(settings.activeProfileId) ?? "Unknown";
        }

        /// <summary>
        /// 全GroupのBuild/Load Pathを設定
        /// </summary>
        private static void SetAllGroupsBuildMode(AddressableAssetSettings settings, bool useRemote)
        {
            int localCount = 0;
            int remoteCount = 0;

            foreach (var group in settings.groups)
            {
                if (group == null) continue;

                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                // Local固定グループは常にLocal
                if (ShouldAlwaysBeLocal(group))
                {
                    SetGroupToLocal(schema);
                    localCount++;
                }
                else
                {
                    // 環境設定に従う
                    if (useRemote)
                    {
                        SetGroupToRemote(schema);
                        remoteCount++;
                    }
                    else
                    {
                        SetGroupToLocal(schema);
                        localCount++;
                    }
                }

                EditorUtility.SetDirty(group);
            }

            Debug.Log($"[Addressables] Group設定完了: Local={localCount}, Remote={remoteCount}");
        }

        /// <summary>
        /// Groupが常にLocalであるべきか判定
        /// </summary>
        private static bool ShouldAlwaysBeLocal(AddressableAssetGroup group)
        {
            var groupName = group.Name;
            return LocalOnlyPatterns.Any(pattern =>
                groupName.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// GroupをLocal設定に変更
        /// </summary>
        private static void SetGroupToLocal(BundledAssetGroupSchema schema)
        {
            schema.BuildPath.SetVariableByName(schema.Group.Settings, "Local.BuildPath");
            schema.LoadPath.SetVariableByName(schema.Group.Settings, "Local.LoadPath");
        }

        /// <summary>
        /// GroupをRemote設定に変更
        /// </summary>
        private static void SetGroupToRemote(BundledAssetGroupSchema schema)
        {
            schema.BuildPath.SetVariableByName(schema.Group.Settings, "Remote.BuildPath");
            schema.LoadPath.SetVariableByName(schema.Group.Settings, "Remote.LoadPath");
        }

        #region 状態取得メソッド

        /// <summary>
        /// 現在のAddressables状態を取得
        /// </summary>
        public static AddressablesCurrentState GetCurrentState()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return new AddressablesCurrentState();
            }

            var state = new AddressablesCurrentState
            {
                ActiveProfileName = settings.profileSettings.GetProfileName(settings.activeProfileId),
                BuildRemoteCatalog = settings.BuildRemoteCatalog,
                // Content.BuildPath / Content.LoadPath を優先、なければ Remote を使用
                ContentBuildPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Content.BuildPath")
                                   ?? settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.BuildPath"),
                ContentLoadPath = settings.profileSettings.GetValueByName(settings.activeProfileId, "Content.LoadPath")
                                  ?? settings.profileSettings.GetValueByName(settings.activeProfileId, "Remote.LoadPath")
            };

            // Group統計
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) continue;

                state.TotalGroups++;
                var buildPathName = schema.BuildPath.GetName(settings);
                // Content または Remote を使用しているグループをカウント
                if (buildPathName.Contains("Content") || buildPathName.Contains("Remote"))
                {
                    state.ContentGroups++;
                }
            }

            return state;
        }

        #endregion
    }

    /// <summary>
    /// 現在のAddressables状態
    /// </summary>
    public class AddressablesCurrentState
    {
        public string ActiveProfileName { get; set; } = "Unknown";
        public bool BuildRemoteCatalog { get; set; }
        public string ContentBuildPath { get; set; } = "";
        public string ContentLoadPath { get; set; } = "";
        public int TotalGroups { get; set; }
        public int ContentGroups { get; set; }
    }
}
