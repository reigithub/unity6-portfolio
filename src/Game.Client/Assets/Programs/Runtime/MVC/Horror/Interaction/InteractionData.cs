using Game.Shared.Enums;

namespace Game.Horror.Interaction
{
    public struct InteractionTargetInfo
    {
        public ObjectCategory ObjectCategory { get; init; }
        public int Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public int Count  { get; init; }
        public string IconAssetName { get; init; }
    }
}
