using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Shared.Extensions
{
    /// <summary>
    /// UI Toolkit Label用の拡張メソッド
    /// </summary>
    public static class UIToolkitLabelExtensions
    {
        /// <summary>
        /// デフォルトの自動サイズ設定
        /// </summary>
        public static class AutoSizeDefaults
        {
            /// <summary>
            /// 最小フォントサイズの比率（元サイズの1/4）
            /// </summary>
            public const float MinFontSizeRatio = 0.25f;

            /// <summary>
            /// フォントサイズ縮小ステップ
            /// </summary>
            public const float FontSizeStep = 2f;

            /// <summary>
            /// フォールバック用のデフォルトフォントサイズ
            /// </summary>
            public const float FallbackFontSize = 14f;
        }

        /// <summary>
        /// テキストを設定し、オーバーフロー時にフォントサイズを段階的に縮小
        /// maxFontSizeを省略すると、USS/スタイルで定義された元のフォントサイズを使用
        /// minFontSizeを省略すると、maxFontSizeの半分を使用
        /// </summary>
        /// <param name="label">対象のLabel</param>
        /// <param name="text">設定するテキスト</param>
        /// <param name="maxFontSize">最大フォントサイズ（省略時: 元のスタイルから取得）</param>
        /// <param name="minFontSize">最小フォントサイズ（省略時: maxFontSizeの半分）</param>
        /// <param name="fontSizeStep">縮小ステップ（デフォルト: 2px）</param>
        public static void SetTextWithAutoSize(
            this Label label,
            string text,
            float? maxFontSize = null,
            float? minFontSize = null,
            float fontSizeStep = AutoSizeDefaults.FontSizeStep)
        {
            if (label == null) return;

            // 元のフォントサイズを取得（resolvedStyleまたはstyleから）
            float originalFontSize = GetOriginalFontSize(label);

            // maxFontSizeが指定されていない場合は元のサイズを使用
            float effectiveMaxFontSize = maxFontSize ?? originalFontSize;

            // minFontSizeが指定されていない場合はmaxの半分を使用
            float effectiveMinFontSize = minFontSize ?? (effectiveMaxFontSize * AutoSizeDefaults.MinFontSizeRatio);

            // テキストを設定し、フォントサイズを最大値にリセット
            label.text = text;
            label.style.fontSize = effectiveMaxFontSize;

            // 設定を保存してコールバック登録
            var settings = new AutoSizeSettings(effectiveMaxFontSize, effectiveMinFontSize, fontSizeStep);
            label.userData = settings;
            label.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        /// <summary>
        /// 元のフォントサイズを取得
        /// </summary>
        private static float GetOriginalFontSize(Label label)
        {
            // resolvedStyleから取得を試みる
            float resolvedSize = label.resolvedStyle.fontSize;
            if (resolvedSize > 0)
            {
                return resolvedSize;
            }

            // styleから取得を試みる
            var styleValue = label.style.fontSize;
            if (styleValue.keyword == StyleKeyword.Undefined && styleValue.value.value > 0)
            {
                return styleValue.value.value;
            }

            // フォールバック
            return AutoSizeDefaults.FallbackFontSize;
        }

        private static void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var label = evt.target as Label;
            if (label == null) return;

            // コールバックを解除（一度だけ実行）
            label.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            // 設定を取得
            var settings = label.userData as AutoSizeSettings;
            if (settings == null) return;

            // 親要素の幅を取得
            var parent = label.parent;
            if (parent == null) return;

            // 利用可能な幅を計算
            float availableWidth = CalculateAvailableWidth(label, parent);

            // 現在のフォントサイズを取得
            float currentFontSize = label.resolvedStyle.fontSize;

            // テキスト幅がオーバーフローしている場合、フォントサイズを縮小
            if (label.resolvedStyle.width > availableWidth && currentFontSize > settings.MinFontSize)
            {
                float newFontSize = Mathf.Max(currentFontSize - settings.FontSizeStep, settings.MinFontSize);
                label.style.fontSize = newFontSize;

                // 再度チェック
                label.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
            else
            {
                // 完了したらuserDataをクリア
                label.userData = null;
            }
        }

        private static float CalculateAvailableWidth(Label label, VisualElement parent)
        {
            float availableWidth = parent.resolvedStyle.width
                                   - parent.resolvedStyle.paddingLeft
                                   - parent.resolvedStyle.paddingRight;

            // 兄弟要素の幅を引く（同じ行にある他の要素）
            foreach (var child in parent.Children())
            {
                if (child != label && child.resolvedStyle.display != DisplayStyle.None)
                {
                    // flex-direction: rowの場合、兄弟の幅を引く
                    if (parent.resolvedStyle.flexDirection == FlexDirection.Row)
                    {
                        availableWidth -= child.resolvedStyle.width;
                        availableWidth -= child.resolvedStyle.marginLeft;
                        availableWidth -= child.resolvedStyle.marginRight;
                    }
                }
            }

            // マージン分も考慮
            availableWidth -= label.resolvedStyle.marginLeft;
            availableWidth -= label.resolvedStyle.marginRight;

            return Mathf.Max(availableWidth, 0f);
        }

        /// <summary>
        /// 自動サイズ設定を保持する内部クラス
        /// </summary>
        private class AutoSizeSettings
        {
            public float MaxFontSize { get; }
            public float MinFontSize { get; }
            public float FontSizeStep { get; }

            public AutoSizeSettings(float maxFontSize, float minFontSize, float fontSizeStep)
            {
                MaxFontSize = maxFontSize;
                MinFontSize = minFontSize;
                FontSizeStep = fontSizeStep;
            }
        }
    }
}