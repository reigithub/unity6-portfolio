using Game.Core.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.UI.Sound
{
    public static class HorrorUISoundMarkerInstaller
    {
        public static void Install(Component root)
        {
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                var target = selectable.gameObject;
                if (selectable is Button)
                    EnsureMarker<HorrorUISoundClickMarker>(target);

                EnsureMarker<HorrorUISoundSelectMarker>(target);

                if (HorrorUISoundValueMarker.IsAttachTarget(selectable))
                    EnsureMarker<HorrorUISoundValueMarker>(target);
            }

            foreach (var tabGroup in root.GetComponentsInChildren<TabGroup>(true))
                EnsureMarker<HorrorUISoundTabMarker>(tabGroup.gameObject);
        }

        private static void EnsureMarker<T>(GameObject target) where T : Component
        {
            // 既にマーカーがある GameObject はスキップ（プレハブ事前設定を優先）
            if (!target.TryGetComponent<T>(out _)) target.AddComponent<T>();
        }
    }
}
