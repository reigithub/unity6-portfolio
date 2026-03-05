#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Shared.Multiplayer
{
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
