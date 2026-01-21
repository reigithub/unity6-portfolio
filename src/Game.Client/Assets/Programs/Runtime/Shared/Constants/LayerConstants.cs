namespace Game.Shared.Constants
{
    public static class LayerConstants
    {
        public static readonly int Default = UnityEngine.LayerMask.NameToLayer("Default");
        public static readonly int Player = UnityEngine.LayerMask.NameToLayer("Player");
        public static readonly int Enemy = UnityEngine.LayerMask.NameToLayer("Enemy");
        public static readonly int Ground = UnityEngine.LayerMask.NameToLayer("Ground");
        public static readonly int Structure = UnityEngine.LayerMask.NameToLayer("Structure");
    }
}