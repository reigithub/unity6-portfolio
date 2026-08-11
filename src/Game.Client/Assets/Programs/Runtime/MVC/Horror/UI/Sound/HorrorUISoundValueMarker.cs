using System;
using Game.Horror.Enums;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// 対話的な Slider（interactable=true）の値変更（onValueChanged）でオプション切替音を要求するマーカー。
    /// 表示ゲージ用の Slider は interactable=false で統一されているため対象から外れる（全数実測済みの判別子）。
    /// R3 の OnValueChangedAsObservable は購読時に現在値を即時発行するが、値の変更ではないため Skip(1) で捨てる
    /// （遅延アクティブ化＝サブタブ等で Awake がいつ走っても購読時発行は無音になる）。
    /// マウスドラッグで毎フレーム値変更が発火するため最小間隔で間引く。ポーズ中（timeScale=0）の
    /// オプション画面でも経過するよう、非スケール時間のプロバイダを明示する。
    /// </summary>
    public class HorrorUISoundValueMarker : MonoBehaviour
    {
        private const float MinPlayInterval = 0.1f;

        private HorrorUISoundPlayer _player;

        /// <summary>このマーカーの付与対象か。</summary>
        public static bool IsAttachTarget(Selectable selectable) => selectable is Slider { interactable: true };

        private void Awake()
        {
            if (!TryGetComponent<Slider>(out var slider)) return;

            slider.OnValueChangedAsObservable()
                .Skip(1) // 購読時の現在値発行は「変更」ではない。ThrottleFirst より前に置く（初回窓の誤消費を防ぐ）
                .ThrottleFirst(TimeSpan.FromSeconds(MinPlayInterval), UnityTimeProvider.UpdateIgnoreTimeScale)
                .Subscribe(_ => NotifyValueChanged())
                .AddTo(this);
        }

        private void NotifyValueChanged()
        {
            _player ??= GetComponentInParent<HorrorUISoundPlayer>();
            if (_player == null) return;

            _player.Play(HorrorUISoundType.ValueChanged);
        }
    }
}
