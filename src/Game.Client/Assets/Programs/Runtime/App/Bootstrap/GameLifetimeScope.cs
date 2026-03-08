using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.App
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<GameRuntimeInitializer>();
            Debug.Log("[GameLifetimeScope] Configure complete");
        }
    }
}
