using Cysharp.Threading.Tasks;
using Game.Shared.Input;
using UnityEngine;

namespace Game.Core.Services
{
    public interface IInputActionIconService
    {
        UniTask LoadAsync();
        void Unload();
        Sprite GetSprite(InputBindingInfo info);
    }
}
