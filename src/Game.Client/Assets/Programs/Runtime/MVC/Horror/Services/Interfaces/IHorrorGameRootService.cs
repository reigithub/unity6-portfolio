using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorGameRootService
    {
        UniTask LoadAsync();
        void Unload();

        Camera Camera { get; }

        UniTask GlobalFadeInAsync(CancellationToken token = default);

        UniTask GlobalFadeOutAsync(CancellationToken token = default);
    }
}
