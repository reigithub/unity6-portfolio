using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core.Services
{
    public interface IInputActionIconService
    {
        UniTask LoadAsync();
        void Unload();
        Sprite GetSprite(string deviceLayoutName, string controlPath);
    }
}
