using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// PlayMode パフォーマンスベンチ共通基底。
    /// ログ出力・targetFrameRate / vSync 設定を揃え、テスト間の環境差を排除する。
    /// </summary>
    public abstract class PlayModeBenchmarkTestBase
    {
        protected StringBuilder LogBuilder;
        protected string LogFilePath;

        private int _originalTargetFrameRate;
        private int _originalVSyncCount;

        protected bool IsBatchMode => Application.isBatchMode;

        [OneTimeSetUp]
        public void BenchOneTimeSetUp()
        {
            var logDir = Path.Combine(Application.dataPath, "..", "Logs", "PerformanceTests");
            Directory.CreateDirectory(logDir);
            LogFilePath = Path.Combine(logDir,
                $"{GetType().Name}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // Warmup ノイズ排除: VSync / targetFrameRate を外す
            _originalTargetFrameRate = Application.targetFrameRate;
            _originalVSyncCount = QualitySettings.vSyncCount;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
        }

        [OneTimeTearDown]
        public void BenchOneTimeTearDown()
        {
            Application.targetFrameRate = _originalTargetFrameRate;
            QualitySettings.vSyncCount = _originalVSyncCount;
        }

        [SetUp]
        public void BenchSetUp()
        {
            LogBuilder = new StringBuilder();
        }

        [TearDown]
        public void BenchTearDown()
        {
            if (LogBuilder != null && LogBuilder.Length > 0)
            {
                var content = LogBuilder.ToString();
                Debug.Log(content);
                File.AppendAllText(LogFilePath, content + "\n");
            }
        }
    }
}
