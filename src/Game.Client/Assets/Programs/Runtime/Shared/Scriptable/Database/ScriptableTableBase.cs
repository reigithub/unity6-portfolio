using UnityEngine;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// <see cref="ScriptableTable{TRecord}"/> の非ジェネリック基底。
    /// CustomEditor が子クラス全体（editorForChildClasses）を対象にできるようにするための型。
    /// 整列・検証は OnValidate での自動実行を行わず、Inspector のボタン / ⋮メニューから手動実行する。
    /// </summary>
    public abstract class ScriptableTableBase : ScriptableObject
    {
#if UNITY_EDITOR
        /// <summary>主キー昇順整列＋空要素除去＋重複警告＋索引キャッシュ無効化（生成 partial が実装）。</summary>
        public abstract void EditorSortAndValidate();

        /// <summary>records が主キー昇順・空要素なしに整っているか（生成 partial が実装）。</summary>
        public abstract bool EditorIsSorted();
#endif
    }
}
