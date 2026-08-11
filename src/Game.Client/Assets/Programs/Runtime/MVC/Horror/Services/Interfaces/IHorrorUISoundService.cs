using Game.Horror.Enums;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorUISoundService : IGameService
    {
        /// <summary>
        /// UI操作効果音を再生する。SE アセット名は呼び出し側が渡す（空文字/null は再生しない）。
        /// Select のみ同一フレーム末尾まで遅延され、同一フレームに他種別が再生された場合は破棄される。
        /// </summary>
        void Play(HorrorUISoundType type, string seAssetName);
    }
}
