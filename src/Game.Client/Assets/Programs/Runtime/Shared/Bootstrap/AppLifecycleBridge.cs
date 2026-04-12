using R3;
using UnityEngine;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// Unity の <see cref="MonoBehaviour.OnApplicationFocus"/> / <see cref="MonoBehaviour.OnApplicationPause"/>
    /// を <see cref="IAppLifecycleSignals"/> として pure C# service に橋渡しする MonoBehaviour。
    ///
    /// <para>
    /// <see cref="Game.MVP.Survivor.DI.SurvivorLifetimeScope"/> の GameObject に AddComponent され、
    /// scope lifecycle に追従して destroy される。静的インスタンスは持たず、DI container 経由でのみ提供される。
    /// </para>
    /// </summary>
    public sealed class AppLifecycleBridge : MonoBehaviour, IAppLifecycleSignals
    {
        private readonly Subject<bool> _focusChanged = new();

        public Observable<bool> OnFocusChanged => _focusChanged;

        private void OnApplicationFocus(bool hasFocus)
        {
            _focusChanged.OnNext(hasFocus);
        }

        private void OnApplicationPause(bool paused)
        {
            // pause 解除 = focused 復帰扱い (mobile 対応)
            if (!paused)
            {
                _focusChanged.OnNext(true);
            }
        }

        private void OnDestroy()
        {
            _focusChanged.OnCompleted();
            _focusChanged.Dispose();
        }
    }
}
