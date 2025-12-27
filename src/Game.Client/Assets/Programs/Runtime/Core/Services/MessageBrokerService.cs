using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.MessagePipe;
using UnityEngine;

namespace Game.Core.Services
{
    public class MessageBrokerService : GameService
    {
        // Subscribeした後に、BuiltinContainerBuilder.BuildServiceProviderすると
        // 設定が全て吹き飛ぶため、同じインスタンスを使い回す事が現実的ではなさそう
        // という事で無理やりインスタンスを分けて管理してみるサービス…
        // オリジナルのMessageBroker作るしかない…のか…？
        // 寿命を短く使う分には、こちらで
        private readonly Dictionary<int, MessageBroker> _messageBrokers = new();

        public MessageBroker GetOrAdd(int key)
        {
            // TODO: Keyを重複登録しようとした際に、気づけるようにしたい
            if (!_messageBrokers.TryGetValue(key, out var messageBroker))
            {
                messageBroker = new MessageBroker();
                _messageBrokers[key] = messageBroker;
            }

            return messageBroker;
        }

        public void Remove(int key)
        {
            _messageBrokers.Remove(key);
        }

        public void RemoveAll()
        {
            _messageBrokers.Clear();
        }

        // BuiltinContainerBuilder.BuildServiceProviderした後に、Subscribeし始める必要があるため
        // AddMessageBroker～BuildServiceProviderを1ヶ所に集約してみる
        public GlobalMessageBroker GlobalMessageBroker { get; private set; } = new();

        protected internal override void Startup()
        {
            // 使うやつは予めココに全て記述する…
            GlobalMessageBroker.AddMessageBroker<int, int>();
            GlobalMessageBroker.AddMessageBroker<int, bool>();
            GlobalMessageBroker.AddMessageBroker<int, GameObject>();
            GlobalMessageBroker.AddMessageBroker<int, UniTaskCompletionSource>();

            GlobalMessageBroker.Build();
        }

        protected internal override void Shutdown()
        {
            RemoveAll();
            GlobalMessageBroker = null;
        }

        protected internal override bool AllowResidentOnMemory => true;
    }
}