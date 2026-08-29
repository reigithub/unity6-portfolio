using Cysharp.Threading.Tasks;
using R3;

namespace Game.Shared.SaveData
{
    public interface ISaveRepository<TData>
    {
        TData Data { get; }
        bool IsLoaded { get; }
        bool IsDirty { get; }
        Observable<TData> OnDataChanged { get; }
        void CreateNewSaveData();
        UniTask LoadAsync();
        UniTask SaveAsync();
        UniTask SaveIfDirtyAsync();
        UniTask DeleteAsync();
        void MarkDirty();
    }
}
