namespace Game.Shared.Constants
{
    public static class LayerMaskConstants
    {
        public static readonly int Default = 1 << LayerConstants.Default;
        public static readonly int Player = 1 << LayerConstants.Player;
        public static readonly int Enemy = 1 << LayerConstants.Enemy;
        public static readonly int Ground = 1 << LayerConstants.Ground;
        public static readonly int Structure = 1 << LayerConstants.Structure;
        public static readonly int Item = 1 << LayerConstants.Item;
    }
}