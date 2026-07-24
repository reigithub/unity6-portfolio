using Cysharp.Threading.Tasks;
using Game.Shared.Services.Interfaces;
using UnityEngine;

namespace Game.Horror.Services.Interfaces
{
    public interface IHorrorIconService : IGameService
    {
        UniTask LoadAsync();
        void Unload();
        Sprite GetSprite(string spriteName);
    }
}
