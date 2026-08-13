using System;
using Game.Horror.Enums;
using UnityEngine;

namespace Game.Horror.UI.Sound
{
    /// <summary>
    /// UI効果音の対応1行分（音種別 × アクション名 × SEアセット名 × 押下継続許可）。
    /// アセット参照を持たない純データで、コード定義の既定表とインスペクタの画面上書き行の両方で使う。
    /// アクション名が空の行はゲートなし（種別→SEアセット名の解決のみ）。
    /// SEアセット名が空の行は「その種別×アクションを無音化する」宣言として扱われる。
    /// allowPressed の行は、アクションが押下/作動を継続している間もゲートを通す
    /// （ドラッグ・リピート等、継続入力そのものが操作である行専用。省略時 false）。
    /// </summary>
    [Serializable]
    public class HorrorUISoundInfo
    {
        [SerializeField] private HorrorUISoundType _type;
        [SerializeField] private string _actionName;
        [SerializeField] private string _seAssetName;
        [SerializeField] private bool _allowPressed;

        public HorrorUISoundInfo(HorrorUISoundType type, string actionName, string seAssetName,
            bool allowPressed = false)
        {
            _type = type;
            _actionName = actionName;
            _seAssetName = seAssetName;
            _allowPressed = allowPressed;
        }

        public HorrorUISoundType Type => _type;
        public string ActionName => _actionName;
        public string SeAssetName => _seAssetName;
        public bool AllowPressed => _allowPressed;
    }
}
