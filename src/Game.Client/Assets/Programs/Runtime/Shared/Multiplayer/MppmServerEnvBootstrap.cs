#if UNITY_EDITOR
using Game.Shared.Environment;
using UnityEngine;

namespace Game.Shared.Multiplayer
{
    /// <summary>
    /// MPPM Server タグ付き Editor インスタンスに対して、.env から
    /// UnityServerConfigFactory が参照する環境変数を現プロセスに注入する。
    /// DedicatedServerEditorMenu と同じ EnvVarHelper API を使い、
    /// スタンドアロン DS .exe と同じ .env 源を MPPM Server でも共有する。
    /// </summary>
    internal static class MppmServerEnvBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyEnvIfMppmServer()
        {
            if (!MppmHelper.IsServer()) return;

            var envFilePath = EnvVarHelper.FindDefaultEnvFile();
            if (envFilePath == null)
            {
                Debug.LogWarning("[MppmServerEnvBootstrap] .env が見つかりません。UnityServerConfigFactory はデフォルト値で起動します");
                return;
            }

            var envVars = EnvVarHelper.Parse(envFilePath);
            int applied = 0;
            int skipped = 0;
            foreach (var kv in envVars)
            {
                // shell 経由で既に注入されている値は尊重（上書きしない）
                if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(kv.Key)))
                {
                    System.Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                    applied++;
                }
                else
                {
                    skipped++;
                }
            }
            Debug.Log($"[MppmServerEnvBootstrap] .env から {applied} 件を注入、{skipped} 件は shell 優先でスキップ: {envFilePath}");
        }
    }
}
#endif
