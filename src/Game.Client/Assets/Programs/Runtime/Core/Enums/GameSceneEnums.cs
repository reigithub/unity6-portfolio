using System;

namespace Game.Core.Enums
{
    /// <summary>
    /// ゲームシーン操作タイプ
    /// </summary>
    [Flags]
    public enum GameSceneOperationType
    {
        None = 0,

        // 次のシーンへ遷移
        TransitionNext = 1 << 0,

        // シーンをSleepモードにする
        Sleep = 1 << 1,

        // 遷移時にクロスフェードする
        CrossFade = 1 << 2,

        // 直前のシーンに戻る
        TransitionPrev = 1 << 3,

        // 遷移したことがあるシーンを指定して戻る
        TransitionBackTo = 1 << 4,

        // シーンの上にオーバーレイ表示する
        Overlay = 1 << 5,

        // オーバーレイを閉じる
        OverlayClose = 1 << 6,
        OverlayCloseAll = 1 << 7,

        // 全てのシーンを終了させる
        TerminateAll = 1 << 8,

        // 遷移履歴を削除
        ClearTransitionHistory = 1 << 9,
    }
}