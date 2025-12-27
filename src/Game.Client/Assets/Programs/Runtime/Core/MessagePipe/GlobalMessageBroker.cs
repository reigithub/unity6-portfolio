using System;
using MessagePipe;

// global provider が staticなので、やむを得ず毎回、自分のインスタンスをセットし直すことで無理やり競合を回避できるが...
// デメリットとして、GlobalMessagePipe.MessagePipeDiagnosticsInfoも入れ替わるので、同時に複数インスタンスが存在していると、MessagePipeDiagnosticsInfoWindowで直前のものしか見る事ができない
// ゲーム内に1つしか持たないという方法もあるが…（→MessageBrokerServiceに持たせたみた）
// 公式ではGlobalMessagePipe推奨らしいが、メリットが未だ曖昧なので分かり次第再考…

namespace Game.Core.MessagePipe
{
    public class GlobalMessageBroker
    {
        private readonly BuiltinContainerBuilder _builder;
        private IServiceProvider _serviceProvider;

        public GlobalMessageBroker()
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
            SetProvider();
        }

        private void SetProvider()
        {
            GlobalMessagePipe.SetProvider(_serviceProvider);
        }

        public IPublisher<TKey, TMessage> GetPublisher<TKey, TMessage>()
        {
            // SetProvider();
            return GlobalMessagePipe.GetPublisher<TKey, TMessage>();
        }

        public ISubscriber<TKey, TMessage> GetSubscriber<TKey, TMessage>()
        {
            // SetProvider();
            return GlobalMessagePipe.GetSubscriber<TKey, TMessage>();
        }

        public IAsyncPublisher<TKey, TMessage> GetAsyncPublisher<TKey, TMessage>()
        {
            // SetProvider();
            return GlobalMessagePipe.GetAsyncPublisher<TKey, TMessage>();
        }

        public IAsyncSubscriber<TKey, TMessage> GetAsyncSubscriber<TKey, TMessage>()
        {
            // SetProvider();
            return GlobalMessagePipe.GetAsyncSubscriber<TKey, TMessage>();
        }
    }
}