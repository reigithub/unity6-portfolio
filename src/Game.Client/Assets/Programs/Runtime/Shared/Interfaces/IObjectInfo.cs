using Game.Shared.Enums;

namespace Game.Shared.Interfaces
{
    public interface IObjectInfo
    {
        ObjectCategory ObjectCategory { get; }
        int ObjectId { get; }
        string Name { get; }
        string Description { get; }
        string IconAssetName { get; }
        int MaxCount { get; }
        bool HasEffect { get; }
    }
}
