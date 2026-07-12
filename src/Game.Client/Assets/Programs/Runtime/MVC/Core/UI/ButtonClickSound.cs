using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.UI
{
    public class ButtonClickSound : MonoBehaviour, IPointerDownHandler
    {
        private IAudioService _audioService;
        private IAudioService AudioService => _audioService ??= GameServiceManager.Resolve<IAudioService>();

        public void OnPointerDown(PointerEventData eventData)
            => AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton).Forget();
    }
}
