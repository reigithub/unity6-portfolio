using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Horror.Interaction;
using UnityEngine;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorGameRootService
    {
        UniTask LoadAsync();
        void Unload();

        Camera Camera { get; }

        /// <summary>インタラクトプロンプト表示の中央プール（貸出 API のみ公開）。</summary>
        IInteractionPromptPool PromptPool { get; }

        UniTask GlobalFadeInAsync(CancellationToken token = default);

        UniTask GlobalFadeOutAsync(CancellationToken token = default);
    }
}
