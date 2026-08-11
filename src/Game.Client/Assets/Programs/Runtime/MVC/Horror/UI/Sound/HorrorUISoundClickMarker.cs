using Game.Horror.Enums;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// Button の onClick（実行成立）で効果音を要求するマーカー。
    /// どの音を鳴らすかは UI 自体が決める: 既定は Submit 音で、キャンセル動作のボタンには Cancel、
    /// 値送り（セレクタ矢印等）には ValueChanged、無機能クリック（長押し実行等）には None をプレハブ側で設定する。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HorrorUISoundClickMarker : MonoBehaviour
    {
        [SerializeField] private HorrorUISoundType _clickSound = HorrorUISoundType.Submit;

        private HorrorUISoundPlayer _player;

        private void Awake()
        {
            if (!TryGetComponent<Button>(out var button)) return;

            button.OnClickAsObservable().Subscribe(_ => OnClick()).AddTo(this);
        }

        private void OnClick()
        {
            if (_clickSound == HorrorUISoundType.None) return;

            // 受付（HorrorUISoundPlayer）は初回イベント時に遅延解決する（動的生成でアタッチ後に親階層へ配置されるケースに対応）
            _player ??= GetComponentInParent<HorrorUISoundPlayer>();
            if (_player == null) return;

            _player.Play(_clickSound);
        }
    }
}
