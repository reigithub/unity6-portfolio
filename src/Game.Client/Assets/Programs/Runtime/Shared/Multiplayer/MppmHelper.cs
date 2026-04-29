#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Shared.Multiplayer
{
    /// <summary>
    /// MPPM 実行時に渡されているコマンドライン引数と CurrentPlayer API の値を診断出力する。
    /// 原因調査用、恒久ではないため使い終わったら削除する。
    /// </summary>
    public static class MppmDiagnostic
    {
        private static bool _logged;

        public static void LogOnce()
        {
            if (_logged) return;
            _logged = true;

            var args = System.Environment.GetCommandLineArgs();
            Debug.Log($"[DIAG][MPPM] CommandLineArgs.Count={args.Length}");
            for (int i = 0; i < args.Length; i++)
            {
                Debug.Log($"[DIAG][MPPM] arg[{i}]={args[i]}");
            }

            try
            {
                var tags = global::Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;
                Debug.Log($"[DIAG][MPPM] CurrentPlayer.Tags=[{string.Join(",", tags)}]");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DIAG][MPPM] CurrentPlayer.Tags access failed: {ex.Message}");
            }

            // IsMainEditor は MPPM 1.4.3 以降に追加（CHANGELOG 記載）。
            // 未定義の場合はコンパイルエラーになる → コンパイル成否で API 存在を検証。
            try
            {
                bool isMainEditor = global::Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor;
                Debug.Log($"[DIAG][MPPM] CurrentPlayer.IsMainEditor={isMainEditor}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DIAG][MPPM] CurrentPlayer.IsMainEditor access failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// MPPM (Multiplayer Play Mode) クローンインスタンスの検出とセーブデータ分離ヘルパー
    /// クローンごとに固有のデータパスを割り当て、セッション・セーブデータの競合を防ぐ
    /// </summary>
    public static class MppmHelper
    {
        private const string CloneFlag = "--virtual-project-clone";
        private const string VpIdPrefix = "-vpId=";

        private static bool? _isClone;
        private static string _cloneId;

        public enum MppmTag { None, Host, Client, Server }

        public static IReadOnlyList<string> GetCurrentPlayerTags()
            => global::Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;

        public static MppmTag ResolveTag()
        {
            var tags = GetCurrentPlayerTags();
            if (tags.Contains("Server"))
                return MppmTag.Server;
            if (tags.Contains("Client"))
                return MppmTag.Client;
            if (tags.Contains("Host"))
                return MppmTag.Host;

            return MppmTag.None;
        }

        public static bool IsActive() => GetCurrentPlayerTags().Count > 0;

        public static bool IsHost() => ResolveTag() == MppmTag.Host;

        public static bool IsClient() => ResolveTag() == MppmTag.Client;

        public static bool IsServer() => ResolveTag() == MppmTag.Server;

        /// <summary>
        /// 現在のプロセスがMPPMクローンかどうか
        /// コマンドライン引数 --virtual-project-clone の有無で判定
        /// </summary>
        public static bool IsClone
        {
            get
            {
                if (!_isClone.HasValue)
                {
                    var args = System.Environment.GetCommandLineArgs();
                    _isClone = args.Contains(CloneFlag);
                }
                return _isClone.Value;
            }
        }

        /// <summary>
        /// クローンの一意識別子を取得
        /// MPPMがクローン起動時に渡す -vpId=... コマンドライン引数から取得
        /// VPクローンを削除しない限り、プレイセッション間で安定した値を返す
        /// </summary>
        public static string CloneId
        {
            get
            {
                if (_cloneId == null)
                {
                    _cloneId = ReadVpIdFromCommandLine();
                    Debug.Log($"[MppmHelper] CloneId resolved: {_cloneId}");
                }
                return _cloneId;
            }
        }

        private static string ReadVpIdFromCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith(VpIdPrefix, StringComparison.Ordinal))
                {
                    return arg.Substring(VpIdPrefix.Length);
                }
            }

            // フォールバック: -vpId が見つからない場合（通常発生しない）
            Debug.LogWarning("[MppmHelper] -vpId not found in command line args, falling back to process ID");
            return $"pid_{System.Diagnostics.Process.GetCurrentProcess().Id}";
        }
    }
}
#endif
