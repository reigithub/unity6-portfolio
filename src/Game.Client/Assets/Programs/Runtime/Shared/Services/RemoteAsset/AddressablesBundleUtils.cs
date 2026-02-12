namespace Game.Shared.Services.RemoteAsset
{
    /// <summary>
    /// Addressablesバンドル関連のユーティリティ
    /// ランタイム、エディタ、CIツールから共通で使用可能
    /// </summary>
    public static class AddressablesBundleUtils
    {
        /// <summary>
        /// ローカル専用バンドルのパターン一覧
        /// </summary>
        private static readonly string[] LocalBundlePatterns =
        {
            "defaultlocalgroup",
            "local_",
            "_local_",
            "monoscripts",
            "unitybuiltinassets"
        };

        /// <summary>
        /// バンドル名またはパスがローカル専用バンドルかどうかを判定
        /// </summary>
        /// <param name="bundleNameOrPath">バンドル名またはパス（internalId, ファイル名, 相対パスなど）</param>
        /// <returns>ローカル専用バンドルの場合true</returns>
        /// <remarks>
        /// 以下のパターンを含む場合はローカルバンドルと判定:
        /// - defaultlocalgroup: Default Local Group のバンドル
        /// - local_: ローカル専用を示すプレフィックス
        /// - _local_: ローカル専用を示すインフィックス
        /// - monoscripts: MonoScript バンドル
        /// - unitybuiltinassets: Unity Built-in Assets バンドル
        /// </remarks>
        public static bool IsLocalBundle(string bundleNameOrPath)
        {
            if (string.IsNullOrEmpty(bundleNameOrPath))
                return false;

            var lowerPath = bundleNameOrPath.ToLowerInvariant();

            foreach (var pattern in LocalBundlePatterns)
            {
                if (lowerPath.Contains(pattern))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// ローカルバンドルパターンの一覧を取得
        /// </summary>
        /// <returns>パターン文字列の配列</returns>
        public static string[] GetLocalBundlePatterns()
        {
            // 配列のコピーを返す（不変性を保証）
            var copy = new string[LocalBundlePatterns.Length];
            LocalBundlePatterns.CopyTo(copy, 0);
            return copy;
        }
    }
}
