using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// プレイヤー入力の抽象化。モードごとに実装を切り替え。
    /// - SP/Host: LocalInputProvider（IInputService から直接読み取り）
    /// - Server: ServerInputProvider（NetworkSurvivorPlayerState バッファから消費）
    /// - Client: ClientInputProvider（IInputService → ServerRpc 送信、ローカル処理なし）
    /// </summary>
    public interface ISurvivorPlayerInputProvider
    {
        /// <summary>
        /// 入力を取得する。
        /// ローカル処理すべき入力がある場合 true を返す。
        /// Client モードでは ServerRpc 送信後 false を返す（ローカル処理不要）。
        /// </summary>
        /// <param name="moveValue">移動量</param>
        /// <param name="isSprinting">スプリント</param>
        /// <param name="cameraRotationY">カメラのY回転角（カメラ準拠移動に使用）</param>
        bool TryGetMoveInput(out Vector2 moveValue, out bool isSprinting, out float cameraRotationY);
    }
}
