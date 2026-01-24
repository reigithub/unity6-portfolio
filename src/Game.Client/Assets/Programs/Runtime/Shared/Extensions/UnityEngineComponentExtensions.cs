namespace Game.Shared.Extensions
{
    public static class UnityEngineComponentExtensions
    {
        public static bool CompareLayer(this UnityEngine.Component component, int layer)
        {
            if (component == null)
                return false;

            return component.gameObject.layer == layer;
        }
    }
}