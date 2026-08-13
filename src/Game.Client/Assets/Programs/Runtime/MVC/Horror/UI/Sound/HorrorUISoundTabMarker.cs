using Game.Core.UI;
using Game.Horror.Enums;
using R3;
using UnityEngine;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// TabGroup の実切替（OnTabChanged）でタブ切替音を要求するマーカー。
    /// 初回通知は初期化として無音で取り込み（Skip）、以後 index が実際に変化した時のみ発音する（DistinctUntilChanged）。
    /// </summary>
    [RequireComponent(typeof(TabGroup))]
    public class HorrorUISoundTabMarker : MonoBehaviour
    {
        private HorrorUISoundPlayer _player;

        private void Awake()
        {
            if (!TryGetComponent<TabGroup>(out var tabGroup)) return;

            tabGroup.OnTabChanged
                .DistinctUntilChanged()
                .Skip(1)
                .Subscribe(_ =>
                {
                    _player ??= GetComponentInParent<HorrorUISoundPlayer>();
                    if (_player == null) return;

                    _player.Play(HorrorUISoundType.TabChanged);
                })
                .AddTo(this);
        }
    }
}
