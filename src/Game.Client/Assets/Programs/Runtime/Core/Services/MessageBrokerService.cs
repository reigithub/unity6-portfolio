using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Contents.Scenes;
using Game.Core.MessagePipe;
using UnityEngine;

namespace Game.Core.Services
{
    public class MessageBrokerService : IMessageBrokerService
    {
        public MessageBrokerService()
        {
        }

        // BuiltinContainerBuilder.BuildServiceProviderした後に、Subscribeし始める必要があるため
        // AddMessageBroker～BuildServiceProviderを1ヶ所に集約してみる
        public GlobalMessageBroker GlobalMessageBroker { get; private set; } = new();

        public void Startup()
        {
            // 使うやつは予めココに全て記述する…
            GlobalMessageBroker.AddMessageBroker<int, int>();
            GlobalMessageBroker.AddMessageBroker<int, int?>();
            GlobalMessageBroker.AddMessageBroker<int, bool>();
            GlobalMessageBroker.AddMessageBroker<int, string>();

            GlobalMessageBroker.AddMessageBroker<int, GameObject>();
            GlobalMessageBroker.AddMessageBroker<int, Collision>();
            GlobalMessageBroker.AddMessageBroker<int, Collider>();
            GlobalMessageBroker.AddMessageBroker<int, Vector2>();
            GlobalMessageBroker.AddMessageBroker<int, Vector3>();
            GlobalMessageBroker.AddMessageBroker<int, Material>();

            GlobalMessageBroker.AddMessageBroker<int, UniTaskCompletionSource<int>>();
            GlobalMessageBroker.AddMessageBroker<int, UniTaskCompletionSource<bool>>();

            // Request/Responseの実験
            GlobalMessageBroker.AddRequestHandler<GameStageResultData, bool, GameStageRequestHandler>();
            GlobalMessageBroker.AddRequestHandler<GameStageTotalResultRequest, GameStageTotalResultData, GameStageRequestHandler>();

            GlobalMessageBroker.Build();
        }

        public void Shutdown()
        {
            RemoveAll();
            GlobalMessageBroker = null;
        }

        #region MessageBroker

        // 任意のタイミングで再ビルドできないので、使わない可能性が高い（ビルドする度に前回までの設定が全て上書きされて無くなるため）
        // MVCにおいてはライフサイクルをカスタムするにはオリジナルで作る必要がある
        private readonly Dictionary<int, MessageBroker> _messageBrokers = new();

        public MessageBroker GetOrAdd(int key)
        {
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

        #endregion
    }
}