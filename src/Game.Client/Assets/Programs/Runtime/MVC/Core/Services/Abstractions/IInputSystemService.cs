using System;
using Game.Shared.Input;

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
