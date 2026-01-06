using System;
using R3;

namespace Game.Core.Extensions
{
    public static class UnityEngineButtonExtensions
    {
        private const double ThrottleFirstIntervalSeconds = 3D;

        public static Observable<Unit> OnClickAsObservableThrottleFirst(this UnityEngine.UI.Button button, double? interval = 3D)
        {
            return button
                .OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(interval ?? ThrottleFirstIntervalSeconds))
                .AsUnitObservable();
        }
    }
}