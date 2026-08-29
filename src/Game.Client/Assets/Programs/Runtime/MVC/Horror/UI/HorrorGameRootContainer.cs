using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Horror.Interaction;
using Game.Horror.Services.Interfaces;
using Game.Shared.Constants;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Horror
{
    /// <summary>
    /// ゲーム全体に関わるオブジェクトを管理する
    /// </summary>
    public class HorrorGameRootContainer : MonoBehaviour
    {
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private PlayerInput _playerInput;
        [SerializeField] private CanvasGroup _fadeCanvasGroup;
        [SerializeField] private GameObject _fpsView;
        [SerializeField] private InteractionPromptPool _promptPool;

        public Camera Camera => _mainCamera;
        public InteractionPromptPool PromptPool => _promptPool;

        public void Initialize()
        {
            // playerInput.controlsChangedEvent.AddListener(UpdateControlScheme);
            // InputSystem.onEvent += (inputEventPtr, device) => { Debug.Log($"InputSystem InputDevice: {device}"); };
            // Keyboard.current / Mouse.current / Gamepad.current / Pointer.current / Touchscreen.current;
            // playerInput.SwitchCurrentControlScheme(InputConstants.Gamepad);

            var inputSystemService = GameServiceManager.Resolve<IInputSystemService>();
            _playerInput.controlsChangedEvent.AsObservable()
                .Subscribe(x => inputSystemService.UpdateControlScheme(x.currentControlScheme))
                .AddTo(this);

            // GameScene
            var messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            messagePipeService.SubscribeAsync<MessageSignals.GameScene.FadeOut>(async (_, token) => await GlobalFadeOutAsync(token))
                .AddTo(this);
            messagePipeService.SubscribeAsync<MessageSignals.GameScene.FadeIn>(async (_, token) => await GlobalFadeInAsync(token))
                .AddTo(this);

            var optionRepository = GameServiceManager.Resolve<IHorrorOptionSaveRepository>();
            optionRepository.OnDataChanged
                .Subscribe(x => SetActiveFpsView(x.ShowFrameRate))
                .AddTo(this);
            SetActiveFpsView(optionRepository.Data.ShowFrameRate);

            _promptPool.Initialize();
        }

        public UniTask GlobalFadeInAsync(CancellationToken token = default)
            => DoFade(UIAnimationConstants.AlphaTransparent, UIAnimationConstants.SceneTransitionFadeOutDuration, token);

        public UniTask GlobalFadeOutAsync(CancellationToken token = default)
            => DoFade(UIAnimationConstants.AlphaOpaque, UIAnimationConstants.SceneTransitionFadeInDuration, token);

        private UniTask DoFade(float endValue, float duration, CancellationToken token = default)
        {
            return _fadeCanvasGroup.DOFade(endValue, duration)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: token);
        }

        private void SetActiveFpsView(bool active)
        {
            if (_fpsView != null) _fpsView.SetActive(active);
        }
    }
}
