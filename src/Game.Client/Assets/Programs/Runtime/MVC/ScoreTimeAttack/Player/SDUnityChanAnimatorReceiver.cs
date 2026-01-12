using Game.Core.MessagePipe;
using Game.Core.Services;
using R3;
using UnityEngine;

namespace Game.ScoreTimeAttack.Player
{
    /// <summary>
    /// SD-Unityちゃん用のアニメーションを外側から操作する
    /// </summary>
    public class SDUnityChanAnimatorReceiver : MonoBehaviour
    {
        [SerializeField]
        private Animator _animator;

        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private void Awake()
        {
            if (TryGetComponent<Animator>(out var animator))
            {
                _animator = animator;
            }

            MessagePipeService.Subscribe<string>(MessageKey.Player.PlayAnimation, stateName =>
                {
                    if (_animator) _animator.Play(stateName);
                })
                .AddTo(this);
        }
    }
}