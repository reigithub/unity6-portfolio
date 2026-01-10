using Game.Core.MessagePipe;

namespace Game.Core.Services
{
    /// <summary>
    /// メッセージブローカーサービスのインターフェース
    /// </summary>
    public interface IMessageBrokerService : IGameService
    {
        GlobalMessageBroker GlobalMessageBroker { get; }
        MessageBroker GetOrAdd(int key);
        void Remove(int key);
        void RemoveAll();
    }
}
