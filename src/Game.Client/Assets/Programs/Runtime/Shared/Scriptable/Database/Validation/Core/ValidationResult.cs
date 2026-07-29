#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 1 テーブル分の検証結果。エラーはレコードを特定するキー（主キー値など）でグループ化して保持する。
    /// <see cref="RecordCount"/> が -1 のときは検証自体が例外で中断したことを表す。
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>結果の表示名。テーブル検証ではレコード型名。</summary>
        public string Name { get; }

        /// <summary>検証したレコード数。検証が例外で中断した場合は -1。</summary>
        public int RecordCount { get; }

        public TimeSpan CheckTime { get; set; }

        // 値は常に List<string>。挿入経路が AddError だけなので取り出し時のキャストが成立する。
        private readonly Dictionary<string, IReadOnlyList<string>> _errors = new();

        public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors => _errors;

        public bool HasErrors => _errors.Count > 0;

        public ValidationResult(string name, int recordCount)
        {
            Name = name;
            RecordCount = recordCount;
        }

        /// <param name="key">エラー箇所を特定するキー。レコードの主キー値など。</param>
        /// <param name="message">エラー内容。</param>
        public void AddError(string key, string message)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key が空です。", nameof(key));
            if (string.IsNullOrEmpty(message)) throw new ArgumentException("message が空です。", nameof(message));

            if (_errors.TryGetValue(key, out var list))
            {
                ((List<string>)list).Add(message);
            }
            else
            {
                _errors.Add(key, new List<string> { message });
            }
        }

        /// <summary>エラーを 1 行 1 件で整形する（Console 出力とテストの失敗メッセージで共通）。</summary>
        public string DescribeErrors()
        {
            var builder = new StringBuilder();
            foreach (var pair in _errors)
            {
                foreach (var message in pair.Value)
                {
                    builder.AppendLine($"  [{pair.Key}] {message}");
                }
            }

            return builder.ToString();
        }
    }
}
#endif
