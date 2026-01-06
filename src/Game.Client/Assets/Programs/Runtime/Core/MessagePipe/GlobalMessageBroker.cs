using System;
using MessagePipe;

namespace Game.Core.MessagePipe
{
    public class GlobalMessageBroker
    {
        private readonly BuiltinContainerBuilder _builder;
        private IServiceProvider _serviceProvider;

        public GlobalMessageBroker()
        {
            _builder = new BuiltinContainerBuilder();
            _builder.AddMessagePipe(configure: options =>
            {
                // オプションを変更…
                // options.DefaultAsyncPublishStrategy = AsyncPublishStrategy.Sequential;
                // options.AddGlobalMessageHandlerFilter<>();
                // options.AddGlobalRequestHandlerFilter<>();
            });
        }

        // public void AddMessagePipe(MessagePipeOptions options)
        // {
        //     _builder.AddMessagePipe(configure: options =>
        //     {
        //     });
        // }

        public void AddMessageBroker<TKey, TMessage>()
        {
            _builder.AddMessageBroker<TKey, TMessage>();
        }

        // Request/Response形式
        public void AddRequestHandler<TRequest, TResponse, THandler>()
            where THandler : IRequestHandler
        {
            _builder.AddRequestHandler<TRequest, TResponse, THandler>();
        }

        public void AddAsyncRequestHandler<TRequest, TResponse, THandler>()
            where THandler : IAsyncRequestHandler
        {
            _builder.AddAsyncRequestHandler<TRequest, TResponse, THandler>();
        }

        public void Build()
        {
            _serviceProvider = _builder.BuildServiceProvider();
            SetProvider();
        }

        private void SetProvider()
        {
            // Memo: GlobalMessagePipe.IsInitializedで初期化済みかはチェック可能（後で検討）
            GlobalMessagePipe.SetProvider(_serviceProvider);
        }

        public IPublisher<TKey, TMessage> GetPublisher<TKey, TMessage>()
        {
            return GlobalMessagePipe.GetPublisher<TKey, TMessage>();
        }

        public ISubscriber<TKey, TMessage> GetSubscriber<TKey, TMessage>()
        {
            return GlobalMessagePipe.GetSubscriber<TKey, TMessage>();
        }

        public IAsyncPublisher<TKey, TMessage> GetAsyncPublisher<TKey, TMessage>()
        {
            return GlobalMessagePipe.GetAsyncPublisher<TKey, TMessage>();
        }

        public IAsyncSubscriber<TKey, TMessage> GetAsyncSubscriber<TKey, TMessage>()
        {
            return GlobalMessagePipe.GetAsyncSubscriber<TKey, TMessage>();
        }

        public IRequestHandler<TRequest, TResponse> GetRequestHandler<TRequest, TResponse>()
        {
            return GlobalMessagePipe.GetRequestHandler<TRequest, TResponse>();
        }

        public static IAsyncRequestHandler<TRequest, TResponse> GetAsyncRequestHandler<TRequest, TResponse>()
        {
            return GlobalMessagePipe.GetAsyncRequestHandler<TRequest, TResponse>();
        }
    }
}