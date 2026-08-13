using System.Collections.Generic;
using UnityEngine;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>
    /// <see cref="ScriptableTable{TRecord}"/> の非ジェネリック基底。
    /// CustomEditor が子クラス全体（editorForChildClasses）を対象にできるようにするための型。
    /// 整列・検証は OnValidate での自動実行を行わず、Inspector のボタン / ⋮メニューから手動実行する。
    /// 二次索引キャッシュの無効化のみ、デシリアライズのたびに自動実行する（下記参照）。
    /// </summary>
    public abstract class ScriptableTableBase : ScriptableObject, ISerializationCallbackReceiver
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        // Inspector 適用・外部編集の再インポートで records が再デシリアライズされても、
        // 非シリアライズの二次索引キャッシュは自動では消えない。ここで無効化しないと、
        // ドメインリロード無効環境（Enter Play Mode Options）の Play へ古い索引が持ち込まれ、
        // 実データと索引が乖離する（FindByDropGroupId が実在行に対して 0 件を返した実障害）。
        void ISerializationCallbackReceiver.OnAfterDeserialize() => InvalidateIndexCaches();

        /// <summary>二次索引キャッシュを破棄する（生成 partial が実装。索引を持たないテーブルは空実装）。</summary>
        protected abstract void InvalidateIndexCaches();

#if UNITY_EDITOR
        /// <summary>主キー昇順整列＋空要素除去＋重複警告＋索引キャッシュ無効化（生成 partial が実装）。</summary>
        public abstract void EditorSortAndValidate();

        /// <summary>records が主キー昇順・空要素なしに整っているか（生成 partial が実装）。</summary>
        public abstract bool EditorIsSorted();

        /// <summary>
        /// CSV/TSV から解析した行を records へ反映する（型非依存。ジェネリック基底が実装）。
        /// <paramref name="mergeByPrimaryKey"/> が true なら主キーマージ（一致=更新・新規=追加・ファイル外=保持）、
        /// false なら総入れ替え。反映後に <see cref="EditorSortAndValidate"/> 相当の整列を行う。
        /// </summary>
        public abstract void EditorImportRows(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows, bool mergeByPrimaryKey);

        /// <summary>records を CSV/TSV 出力用のヘッダ＋行へ変換する（型非依存。ジェネリック基底が実装）。</summary>
        public abstract (string[] headers, List<string[]> rows) EditorExportRows();
#endif
    }
}
