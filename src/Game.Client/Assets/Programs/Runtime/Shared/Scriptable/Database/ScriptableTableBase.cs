using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Scriptable.Database
{
#if UNITY_EDITOR
    /// <summary>CSV/TSV インポート時の既存 records への反映方針。</summary>
    public enum ScriptableTableImportMode
    {
        /// <summary>records をファイル内容で総入れ替えする。</summary>
        Replace,

        /// <summary>主キー一致は更新・新規は追加・ファイルに無い既存行は保持する。</summary>
        MergeByPrimaryKey,
    }
#endif

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

        /// <summary>
        /// CSV/TSV から解析した行を records へ反映する（型非依存。ジェネリック基底が実装）。
        /// 反映後に <see cref="EditorSortAndValidate"/> 相当の整列を行う。
        /// </summary>
        public abstract void EditorImportRows(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, ScriptableTableImportMode mode);

        /// <summary>records を CSV/TSV 出力用のヘッダ＋行へ変換する（型非依存。ジェネリック基底が実装）。</summary>
        public abstract (string[] headers, List<string[]> rows) EditorExportRows();
#endif
    }
}
