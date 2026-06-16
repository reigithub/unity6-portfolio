using System;
using Game.Shared.Input;
using UnityEngine.InputSystem;

namespace Game.Core.Services
{
    /// <summary>
    /// 入力サービスインターフェース
    /// ゲーム全体で共有されるInputSystemのUI入力を提供
    /// </summary>
    public interface IInputSystemService : IGameService
    {
        /// <summary>
        /// プレイヤー入力アクション（移動、ジャンプ、攻撃等）
        /// </summary>
        ProjectDefaultInputSystem.PlayerActions Player { get; }

        /// <summary>
        /// UI入力アクション（メニュー操作、決定、キャンセル等）
        /// </summary>
        ProjectDefaultInputSystem.UIActions UI { get; }

        /// <summary>
        /// リバインド対象となる InputActionAsset 実体（このインスタンス上でゲームが動作する）。
        /// </summary>
        InputActionAsset Asset { get; }

        /// <summary>
        /// 指定アクション・スキームの現在のバインド表示文字列を取得する。
        /// コンポジット（WASD 等）は各パートを "/" 区切りで結合して返す。
        /// <paramref name="partName"/> を指定すると、コンポジットの該当パート1つのみの表示を返す。
        /// </summary>
        string GetBindingDisplayString(string scheme, string actionName, string partName = null);

        /// <summary>
        /// 指定アクション・スキームに対するインタラクティブリバインドを開始する。
        /// <paramref name="partName"/> が空ならコンポジットは各パートを順にリバインドし、指定時はその1パートのみをリバインドする。
        /// 同一スキーム内でキーが重複した場合は、相手バインドへターゲットの旧キーを渡して入れ替える（swap）。
        /// 戻り値を Dispose すると進行中のリバインドをキャンセルする。
        /// </summary>
        /// <param name="scheme">コントロールスキーム（Keyboard&amp;Mouse / Gamepad）</param>
        /// <param name="actionName">Player マップのアクション名</param>
        /// <param name="partName">コンポジットのパート名（up/down/left/right）。空＝全体/単体</param>
        /// <param name="onComplete">確定後に呼ばれる。引数は確定後の表示文字列</param>
        /// <param name="onCanceled">キャンセル時に呼ばれる</param>
        IDisposable StartRebind(string scheme, string actionName, string partName, Action<string> onComplete, Action onCanceled);

        /// <summary>
        /// 現在のバインドオーバーライドを JSON 文字列として取得する（永続化用）。
        /// </summary>
        string SaveBindingOverridesAsJson();

        /// <summary>
        /// JSON 文字列からバインドオーバーライドを復元・適用する。空/null は無視する。
        /// </summary>
        void LoadBindingOverrides(string json);

        /// <summary>
        /// 指定アクション・スキームのバインドオーバーライドを既定へ戻す。
        /// <paramref name="partName"/> を指定すると、コンポジットの該当パートのみを戻す。
        /// </summary>
        void ResetBinding(string scheme, string actionName, string partName = null);

        /// <summary>
        /// 全アクションのバインドオーバーライドを既定へ戻す。
        /// </summary>
        void ResetAllBindings();

        /// <summary>
        /// プレイヤー入力を有効化する
        /// </summary>
        void EnablePlayer();

        /// <summary>
        /// プレイヤー入力を無効化する（メニュー表示中等）
        /// </summary>
        void DisablePlayer();

        /// <summary>
        /// プレイヤー入力を一時無効化するスコープ
        /// </summary>
        IDisposable BlockPlayer();

        /// <summary>
        /// UI入力を一時無効化するスコープ
        /// </summary>
        IDisposable BlockUI();

        /// <summary>
        /// UI入力を有効化する
        /// </summary>
        void EnableUI();

        /// <summary>
        /// UI入力を無効化する
        /// </summary>
        void DisableUI();
    }
}
