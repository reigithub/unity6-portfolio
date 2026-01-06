using Game.Core.MessagePipe;
using Game.Core.Services;
using MessagePipe;
using R3;
using UnityEngine;

namespace Game.Contents.Player
{
    /// <summary>
    /// SD-Unityちゃん用のアニメーションを外側から操作する
    /// </summary>
    public class SDUnityChanAnimatorReceiver : MonoBehaviour
    {
        [SerializeField]
        private Animator _animator;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        private void Awake()
        {
            if (TryGetComponent<Animator>(out var animator))
            {
                _animator = animator;
            }

            GlobalMessageBroker.GetSubscriber<int, string>()
                .Subscribe(MessageKey.Player.PlayAnimation, handler: stateName =>
                {
                    if (_animator) _animator.Play(stateName);
                })
                .AddTo(this);
        }
    }
}