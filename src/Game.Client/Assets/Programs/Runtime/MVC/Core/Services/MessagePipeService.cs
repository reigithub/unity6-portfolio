using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using Game.Shared.Events;
using UnityEngine;

namespace Game.Core.Services
{
    /// <summary>
    /// MessagePipeを使用したメッセージサービス
    /// Publish/Subscribe機能を提供する
    /// </summary>
    public class MessagePipeService : IMessagePipeService
    {
        private readonly BuiltinContainerBuilder _builder;
        private IServiceProvider _serviceProvider;

        public MessagePipeService()
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

        public void Startup()
        {
            // 使用するメッセージタイプを登録
            RegisterMessageBrokers();
            Build();
        }

        public void Shutdown()
        {
            _serviceProvider = null;
        }

        #region Registration

        private void RegisterMessageBrokers()
        {
            // 基本型
            _builder.AddMessageBroker<int, int>();
            _builder.AddMessageBroker<int, int?>();
            _builder.AddMessageBroker<int, float>();
            _builder.AddMessageBroker<int, bool>();
            _builder.AddMessageBroker<int, string>();

            // Unity型
            _builder.AddMessageBroker<int, GameObject>();
            _builder.AddMessageBroker<int, Collision>();
            _builder.AddMessageBroker<int, Collider>();
            _builder.AddMessageBroker<int, Vector2>();
            _builder.AddMessageBroker<int, Vector3>();
            _builder.AddMessageBroker<int, Material>();

            // UniTask型
            _builder.AddMessageBroker<int, UniTaskCompletionSource<int>>();
            _builder.AddMessageBroker<int, UniTaskCompletionSource<bool>>();

            // 音イベント
            _builder.AddMessageBroker<NoiseEvent>();

            // _builder.AddRequestHandler<TRequest, TResponse, THandler>();
        }

        private void Build()
        {
            _serviceProvider = _builder.BuildServiceProvider();
            GlobalMessagePipe.SetProvider(_serviceProvider);
        }

        #endregion

        #region Signal Methods (値なしのイベント通知用)

        /// <summary>
        /// シグナルをPublish（値なし）
        /// </summary>
        public void Publish(int key)
        {
            Publish(key, true);
        }

        /// <summary>
        /// シグナルを非同期Publish (Fire and Forget)
        /// </summary>
        public void PublishForget(int key)
        {
            PublishForget(key, true);
        }

        /// <summary>
        /// シグナルを非同期Publish (await可能)
        /// </summary>
        public UniTask PublishAsync(int key, CancellationToken ct = default)
        {
            return PublishAsync(key, true, ct);
        }

        /// <summary>
        /// シグナルをSubscribe（値なし）
        /// </summary>
        public IDisposable Subscribe(int key, Action handler)
        {
            return Subscribe<bool>(key, _ => handler());
        }

        /// <summary>
        /// シグナルを非同期Subscribe（値なし）
        /// </summary>
        public IDisposable SubscribeAsync(int key, Func<CancellationToken, UniTask> handler)
        {
            return SubscribeAsync<bool>(key, (_, ct) => handler(ct));
        }

        #endregion

        #region Message Methods (値ありのメッセージ送受信用)

        /// <summary>
        /// メッセージをPublish
        /// </summary>
        public void Publish<TMessage>(TMessage message)
        {
            GetPublisher<TMessage>().Publish(message);
        }

        /// <summary>
        /// メッセージをPublish
        /// </summary>
        public void Publish<TMessage>(int key, TMessage message)
        {
            GetPublisher<int, TMessage>().Publish(key, message);
        }

        /// <summary>
        /// メッセージをSubscribe
        /// </summary>
        public IDisposable Subscribe<TMessage>(Action<TMessage> handler)
        {
            return GetSubscriber<TMessage>().Subscribe(handler);
        }

        /// <summary>
        /// メッセージをSubscribe
        /// </summary>
        public IDisposable Subscribe<TMessage>(int key, Action<TMessage> handler)
        {
            return GetSubscriber<int, TMessage>().Subscribe(key, handler);
        }

        /// <summary>
        /// メッセージを非同期Publish (Fire and Forget)
        /// </summary>
        public void PublishForget<TMessage>(int key, TMessage message)
        {
            GetAsyncPublisher<int, TMessage>().Publish(key, message);
        }

        /// <summary>
        /// メッセージを非同期Publish (await可能)
        /// </summary>
        public UniTask PublishAsync<TMessage>(int key, TMessage message, CancellationToken ct = default)
        {
            return GetAsyncPublisher<int, TMessage>().PublishAsync(key, message, ct);
        }

        /// <summary>
        /// メッセージを非同期Subscribe
        /// </summary>
        public IDisposable SubscribeAsync<TMessage>(int key, Func<TMessage, CancellationToken, UniTask> handler)
        {
            return GetAsyncSubscriber<int, TMessage>().Subscribe(key, handler);
        }

        #endregion

        #region Raw Accessors

        public IPublisher<TMessage> GetPublisher<TMessage>()
        {
            return GlobalMessagePipe.GetPublisher<TMessage>();
        }

        public IPublisher<TKey, TMessage> GetPublisher<TKey, TMessage>()
        {
            return GlobalMessagePipe.GetPublisher<TKey, TMessage>();
        }

        public ISubscriber<TMessage> GetSubscriber<TMessage>()
        {
            return GlobalMessagePipe.GetSubscriber<TMessage>();
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

        public IAsyncRequestHandler<TRequest, TResponse> GetAsyncRequestHandler<TRequest, TResponse>()
        {
            return GlobalMessagePipe.GetAsyncRequestHandler<TRequest, TResponse>();
        }

        #endregion
    }
}
