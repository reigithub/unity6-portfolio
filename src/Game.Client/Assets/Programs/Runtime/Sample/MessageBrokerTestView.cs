using System;
using MessagePipe;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class MessageBrokerTestView : MonoBehaviour
    {
        [SerializeField] private Button _button;

        private int _count;

        private readonly MessageBroker _messageBroker = new();
        private readonly MessageBroker _messageBroker2 = new();
        private readonly GlobalMessageBroker _globalMessageBroker = new();
        private readonly GlobalMessageBroker _globalMessageBroker2 = new();

        private void OnEnable()
        {
            _messageBroker.AddMessageBroker<int, MessageBrokerTestValue>();
            _messageBroker.Build();
            _messageBroker.GetSubscriber<int, MessageBrokerTestValue>()
                .Subscribe(key: 0, handler: msg => { Debug.LogWarning($"Message 1: {msg.Value}"); }, new MessageHandlerFilterTestValue())
                .AddTo(this);

            _messageBroker2.AddMessageBroker<int, string>();
            _messageBroker2.Build();
            _messageBroker2.GetSubscriber<int, string>()
                .Subscribe(key: 0, handler: msg => { Debug.LogWarning($"Message 2: {msg}"); }, new MessageHandlerFilterTestString())
                .AddTo(this);

            _globalMessageBroker.AddMessageBroker<int, MessageBrokerTestValue>();
            _globalMessageBroker.Build();
            _globalMessageBroker.GetSubscriber<int, MessageBrokerTestValue>()
                .Subscribe(key: 0, handler: msg => { Debug.LogWarning($"Global Message 1: {msg.Value}"); })
                .AddTo(this);

            _globalMessageBroker2.AddMessageBroker<int, string>();
            _globalMessageBroker2.Build();
            _globalMessageBroker2.GetSubscriber<int, string>()
                .Subscribe(key: 0, handler: msg => { Debug.LogWarning($"Global Message 2: {msg}"); })
                .AddTo(this);

            _button.onClick.AddListener(() =>
            {
                _count++;

                _messageBroker.GetPublisher<int, MessageBrokerTestValue>().Publish(0, new MessageBrokerTestValue(_count));
                _messageBroker2.GetPublisher<int, string>().Publish(0, _count.ToString());

                _globalMessageBroker.GetPublisher<int, MessageBrokerTestValue>().Publish(0, new MessageBrokerTestValue(_count));
                _globalMessageBroker2.GetPublisher<int, string>().Publish(0, _count.ToString());
            });
        }
    }

    public class MessageBrokerTestValue
    {
        public int Value { get; set; }

        public MessageBrokerTestValue(int value)
        {
            Value = value;
        }
    }

    public class MessageHandlerFilterTestValue : MessageHandlerFilter<MessageBrokerTestValue>
    {
        public override void Handle(MessageBrokerTestValue msg, Action<MessageBrokerTestValue> next)
        {
            Debug.LogWarning($"---Message Start---");
            next.Invoke(msg);
            Debug.LogWarning($"---Message End---");
        }
    }

    public class MessageHandlerFilterTestString : MessageHandlerFilter<string>
    {
        public override void Handle(string msg, Action<string> next)
        {
            Debug.LogWarning($"---Message Start---");
            next.Invoke(msg);
            Debug.LogWarning($"---Message End---");
        }
    }
}