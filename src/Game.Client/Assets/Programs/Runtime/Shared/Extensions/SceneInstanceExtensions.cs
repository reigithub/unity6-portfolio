using UnityEngine.ResourceManagement.ResourceProviders;

namespace Game.Shared.Extensions
{
    public static class SceneInstanceExtensions
    {
        public static bool CanUnload(this SceneInstance sceneInstance)
            => sceneInstance.Scene.IsValid() && sceneInstance.Scene.isLoaded;
    }
}
