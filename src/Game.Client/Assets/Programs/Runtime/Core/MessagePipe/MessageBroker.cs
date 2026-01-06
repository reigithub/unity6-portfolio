using System;
using MessagePipe;

// GlobalMessagePipeに投げないで自分で制御するver.
// UniRx.MessageBrokerとほぼ同じように使える（はず）

// サンプル実装コード
// private readonly MessageBroker _messageBroker = new();
// _messageBroker.AddMessageBroker<int, MessageBrokerTestValue>();
// _messageBroker.Build();
// _messageBroker.GetSubscriber<int, MessageBrokerTestValue>()
//     .Subscribe(key: 0, handler: msg => { Debug.LogWarning($"Message 1: {msg.Value}"); }, new MessageHandlerFilterTestValue())
//     .AddTo(this); // R3機能
// _messageBroker.GetPublisher<int, MessageBrokerTestValue>().Publish(0, new MessageBrokerTestValue(_count));

namespace Game.Core.MessagePipe
{
    public class MessageBroker
    {
        private readonly BuiltinContainerBuilder _builder;

        private IServiceProvider _serviceProvider;
        // private EventFactory _eventFactory;
        // private MessagePipeDiagnosticsInfo _diagnosticsInfo;

        public MessageBroker()
        {
            _builder = new BuiltinContainerBuilder();
            _builder.AddMessagePipe();
        }

        public void AddMessageBroker<TKey, TMessage>()
        {
            _builder.AddMessageBroker<TKey, TMessage>();
        }

        public void Build()
        {
            _serviceProvider = _builder.BuildServiceProvider();
            // _eventFactory = _serviceProvider.GetRequiredService<EventFactory>();
            // _diagnosticsInfo = _serviceProvider.GetRequiredService<MessagePipeDiagnosticsInfo>();
        }

        public IPublisher<TKey, TMessage> GetPublisher<TKey, TMessage>()
        {
            return _serviceProvider.GetRequiredService<IPublisher<TKey, TMessage>>();
        }

        public ISubscriber<TKey, TMessage> GetSubscriber<TKey, TMessage>()
        {
            return _serviceProvider.GetRequiredService<ISubscriber<TKey, TMessage>>();
        }

        public IAsyncPublisher<TKey, TMessage> GetAsyncPublisher<TKey, TMessage>()
        {
            return _serviceProvider.GetRequiredService<IAsyncPublisher<TKey, TMessage>>();
        }

        public IAsyncSubscriber<TKey, TMessage> GetAsyncSubscriber<TKey, TMessage>()
        {
            return _serviceProvider.GetRequiredService<IAsyncSubscriber<TKey, TMessage>>();
        }
    }
}