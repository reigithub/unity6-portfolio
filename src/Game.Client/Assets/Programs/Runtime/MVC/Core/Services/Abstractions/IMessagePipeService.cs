using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using MessagePipe;

namespace Game.Core.Services
{
    /// <summary>
    /// メッセージブローカーサービスのインターフェース
    /// MessagePipeのPublish/Subscribe機能を提供する
    /// </summary>
    public interface IMessagePipeService : IGameService
    {
        #region Signal Methods (値なしのイベント通知用)

        // Signal Publish (値なし)
        void Publish(int key);
        void PublishForget(int key);
        UniTask PublishAsync(int key, CancellationToken ct = default);

        // Signal Subscribe (値なし)
        IDisposable Subscribe(int key, Action handler);
        IDisposable SubscribeAsync(int key, Func<CancellationToken, UniTask> handler);

        #endregion

        #region Message Methods (値ありのメッセージ送受信用)

        // Message Publish
        void Publish<TMessage>(int key, TMessage message);
        void PublishForget<TMessage>(int key, TMessage message);
        UniTask PublishAsync<TMessage>(int key, TMessage message, CancellationToken ct = default);

        // Message Subscribe
        IDisposable Subscribe<TMessage>(int key, Action<TMessage> handler);
        IDisposable SubscribeAsync<TMessage>(int key, Func<TMessage, CancellationToken, UniTask> handler);

        #endregion

        #region Raw Accessors

        IPublisher<TKey, TMessage> GetPublisher<TKey, TMessage>();
        ISubscriber<TKey, TMessage> GetSubscriber<TKey, TMessage>();
        IAsyncPublisher<TKey, TMessage> GetAsyncPublisher<TKey, TMessage>();
        IAsyncSubscriber<TKey, TMessage> GetAsyncSubscriber<TKey, TMessage>();

        #endregion
    }
}