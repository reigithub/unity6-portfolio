using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

namespace Game.Editor.Build
{
    /// <summary>
    /// Build Profile 関連のユーティリティ
    /// </summary>
    public static class BuildProfileHelper
    {
        private const string BuildProfilesFolder = "Assets/Settings/Build Profiles";

        /// <summary>
        /// 環境別のスクリプティング定義シンボル
        /// </summary>
        public static string GetEnvironmentSymbol(string gameEnvironment)
        {
            return gameEnvironment?.ToUpperInvariant() switch
            {
                "RELEASE" => "RELEASE",
                "REVIEW" => "REVIEW",
                "STAGING" => "STAGING",
                "DEVELOP" => "DEVELOP",
                "LOCAL" => "DEVELOP",  // Local は DEVELOP 扱い
                _ => "DEVELOP"         // デフォルト
            };
        }

        /// <summary>
        /// Build Profile を検索
        /// </summary>
        public static string FindBuildProfilePath(BuildTarget target, string variant = null)
        {
            var platformName = GetPlatformName(target);
            var searchPattern = string.IsNullOrEmpty(variant)
                ? $"{platformName} - Release"
                : $"{platformName} - {variant}";

            var guids = AssetDatabase.FindAssets($"t:BuildProfile {searchPattern}",
                new[] { BuildProfilesFolder });

            if (guids.Length > 0)
            {
                return AssetDatabase.GUIDToAssetPath(guids[0]);
            }

            Debug.LogWarning($"[BuildProfile] Profile not found: {searchPattern}");
            return null;
        }

        /// <summary>
        /// Build Profile を読み込み
        /// </summary>
        public static BuildProfile LoadBuildProfile(string profilePath)
        {
            if (string.IsNullOrEmpty(profilePath))
                return null;

            return AssetDatabase.LoadAssetAtPath<BuildProfile>(profilePath);
        }

        /// <summary>
        /// コマンドライン引数から Build Profile パスを取得
        /// </summary>
        public static string GetBuildProfileFromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-buildProfile" || args[i] == "--buildProfile")
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        /// <summary>
        /// スクリプティング定義シンボルに環境シンボルを追加
        /// </summary>
        public static void ApplyEnvironmentSymbols(BuildTargetGroup targetGroup)
        {
            var gameEnv = Environment.GetEnvironmentVariable("GAME_ENVIRONMENT");
            if (string.IsNullOrEmpty(gameEnv))
            {
                Debug.Log("[BuildProfile] GAME_ENVIRONMENT not set, using DEVELOP");
                gameEnv = "DEVELOP";
            }

            var envSymbol = GetEnvironmentSymbol(gameEnv);
            var currentSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

            // 既存の環境シンボルを削除
            var symbols = currentSymbols
                .Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s) && !IsEnvironmentSymbol(s))
                .ToList();

            // 新しい環境シンボルを追加
            symbols.Insert(0, envSymbol);

            var newSymbols = string.Join(";", symbols);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newSymbols);

            Debug.Log($"[BuildProfile] Environment: {gameEnv} -> Symbol: {envSymbol}");
            Debug.Log($"[BuildProfile] Scripting Define Symbols: {newSymbols}");
        }

        private static bool IsEnvironmentSymbol(string symbol)
        {
            return symbol == "RELEASE" || symbol == "REVIEW" ||
                   symbol == "STAGING" || symbol == "DEVELOP";
        }

        private static string GetPlatformName(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows64 => "Windows",
                BuildTarget.StandaloneLinux64 => "Linux",
                BuildTarget.StandaloneOSX => "macOS",
                BuildTarget.WebGL => "WebGL",
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "iOS",
                _ => target.ToString()
            };
        }
    }
}
