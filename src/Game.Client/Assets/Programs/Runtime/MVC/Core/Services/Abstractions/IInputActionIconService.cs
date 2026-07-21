using Cysharp.Threading.Tasks;
using Game.Shared.Input;
using Game.Shared.Services.Interfaces;
using UnityEngine;

namespace Game.Core.Services
{
    public interface IInputActionIconService : IGameService
    {
        UniTask LoadAsync();
        void Unload();
        Sprite GetSprite(InputBindingInfo info);
    }
}
