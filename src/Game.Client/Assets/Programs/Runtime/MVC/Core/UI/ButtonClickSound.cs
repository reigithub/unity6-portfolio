using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Core.UI
{
    public class ButtonClickSound : MonoBehaviour, IPointerDownHandler
    {
        private AudioService _audioService;
        private AudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        public void OnPointerDown(PointerEventData eventData)
            => AudioService.PlayRandomOneAsync(AudioCategory.SoundEffect, AudioPlayTag.UIButton).Forget();
    }
}
