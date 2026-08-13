using System.Collections.Generic;
using Game.Horror.Constants;
using Game.Horror.Enums;
using Game.Shared.Input;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// UI操作効果音の既定対応表。アクション名の列は発音の起点ではなく、
    /// 「マーカーの発火がユーザー入力に起因すること」を要求するゲート条件
    /// （＝プログラム起因の発火を落とす絞り込み）。行のゲートは「そのアクションが本フレーム performed であること」
    /// （因果の厳密な近似）。allowPressed を宣言した行は押下/作動の継続中も通す
    /// （ドラッグ・キー押しっぱなしのリピート等、継続入力そのものが操作である行専用。
    /// その入力の継続中はプログラム起因の発火も通す穴を受け入れる）。
    /// 空アクションの行はゲートなしで、種別→SEアセット名の解決のみを行う
    /// （Select の因果判定はマーカー側が担う。条件の詳細は HorrorUISoundSelectMarker の doc を参照）。
    /// 「行が無い組は鳴らない」が仕様であり、行の不在も設計判断:
    /// ValueChanged に Point や空アクションの行を置かないのは、プログラム起因の値復元（ダイアログ開幕の初期化等）を
    /// 通さないため。セレクタ矢印ボタンは UI 自体が ClickMarker で ValueChanged を宣言し、Click の Performed 行で解決する
    /// （Slider 側 ValueMarker の要求と同フレームに重なるが、Service の同フレーム同種別畳み込みが1回にする）。
    /// アクション名は生成クラスのプロパティ名（＝アクション名）を nameof で参照し、リネームをコンパイル時に検出する。
    /// </summary>
    public static class HorrorUISoundTable
    {
        public static readonly IReadOnlyList<HorrorUISoundInfo> DefaultRows = new HorrorUISoundInfo[]
        {
            // 選択: 因果判定はマーカー側で確定済みのため、ここは名前解決のみ（空アクション＝ゲートなし）
            new(HorrorUISoundType.Select, string.Empty, HorrorAudioConstants.UISelectSfx),

            // 実行: F・パッドA / 左クリック
            new(HorrorUISoundType.Submit, nameof(ProjectInputActions.UIActions.Submit), HorrorAudioConstants.UISubmitSfx),
            new(HorrorUISoundType.Submit, nameof(ProjectInputActions.UIActions.Click), HorrorAudioConstants.UISubmitSfx),
            new(HorrorUISoundType.Submit, nameof(ProjectInputActions.UIActions.Point), HorrorAudioConstants.UISubmitSfx),

            // キャンセル（マーカー経路）: Cancel 入力 / キャンセルボタンの実行・クリック
            new(HorrorUISoundType.Cancel, nameof(ProjectInputActions.UIActions.Cancel), HorrorAudioConstants.UICancelSfx),
            new(HorrorUISoundType.Cancel, nameof(ProjectInputActions.UIActions.Submit), HorrorAudioConstants.UICancelSfx),
            new(HorrorUISoundType.Cancel, nameof(ProjectInputActions.UIActions.Click), HorrorAudioConstants.UICancelSfx),

            // タブ切替: Q・E / サブタブ左右 / タブ直接クリック / タブへフォーカスして実行
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Next), HorrorAudioConstants.UITabChangedSfx),
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Previous), HorrorAudioConstants.UITabChangedSfx),
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Next2), HorrorAudioConstants.UITabChangedSfx),
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Previous2), HorrorAudioConstants.UITabChangedSfx),
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Click), HorrorAudioConstants.UITabChangedSfx),
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Submit), HorrorAudioConstants.UITabChangedSfx),
            new(HorrorUISoundType.TabChanged, nameof(ProjectInputActions.UIActions.Point), HorrorAudioConstants.UITabChangedSfx),

            // オプション切替: パッド左右（押しっぱなしリピート含む） / ドラッグ（左ボタン押下中） / 矢印ボタンクリック（解放フレームの performed）
            // → 継続入力も操作なので allowPressed（performed に加えて押下/作動の継続中も通す）
            new(HorrorUISoundType.ValueChanged, nameof(ProjectInputActions.UIActions.Navigate), HorrorAudioConstants.UIValueChangedSfx, allowPressed: true),
            new(HorrorUISoundType.ValueChanged, nameof(ProjectInputActions.UIActions.Click), HorrorAudioConstants.UIValueChangedSfx, allowPressed: true),
            new(HorrorUISoundType.ValueChanged, nameof(ProjectInputActions.UIActions.Submit), HorrorAudioConstants.UIValueChangedSfx),
        };
    }
}
