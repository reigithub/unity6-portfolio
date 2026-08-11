using Game.Core.Services;
using Game.Horror.Enums;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// 選択変化（ISelectHandler.OnSelect）で選択音を要求するマーカー。
    /// 発火点は選択の着地点なので、PointerEventReceiver のリダイレクト構造
    /// （ホバー面と選択先が別 GameObject）でもそのまま成立する。
    /// OnSelect はプログラム起因のフォーカス設定（初期フォーカス・タブ切替等）でも発火するため、
    /// ユーザー起因の場合のみ鳴らすようゲートする:
    /// マウス起因＝この選択変更が PointerEventData を伴って設定されたこと
    /// （PointerEventReceiver の中継・Selectable 自身の押下選択の両方で成立。プログラム起因は
    /// EventSystem 既定の BaseEventData が届くため型で区別できる）、
    /// かつ本フレームに実ポインタ移動（Point の値変化）があること。
    /// ナビ起因＝Navigate が押下中であること。
    /// </summary>
    public class HorrorUISoundSelectMarker : MonoBehaviour, ISelectHandler
    {
        private HorrorUISoundPlayer _player;
        private IInputSystemService _inputService;

        public void OnSelect(BaseEventData eventData)
        {
            _inputService ??= GameServiceManager.Resolve<IInputSystemService>();

            // Point は位置コントロールのため IsPressed は常時 true になり判定に使えない。値変化（performed）で移動を見る
            bool mouseCaused = eventData is PointerEventData
                               && _inputService.UI.Point.WasPerformedThisFrame();

            // 押下継続（IsPressed）で判定する: 押しっぱなしのリピート移動では Navigate の performed が発火しない
            bool navCaused = _inputService.UI.Navigate.IsPressed();

            if (!mouseCaused && !navCaused) return;

            _player ??= GetComponentInParent<HorrorUISoundPlayer>();
            if (_player == null) return;

            _player.Play(HorrorUISoundType.Select);
        }
    }
}
