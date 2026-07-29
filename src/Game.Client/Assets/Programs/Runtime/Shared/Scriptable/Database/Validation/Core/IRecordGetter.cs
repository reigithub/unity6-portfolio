#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>検証中のレコード参照口。validator はここ以外からマスターデータを読まない。</summary>
    public interface IRecordGetter
    {
        /// <summary>
        /// テーブルの全レコード。呼び出しごとにビューを生成するため、レコードのループ内で呼ばず
        /// ループ外で 1 回取得して使い回すこと。
        /// </summary>
        /// <exception cref="InvalidOperationException">対象テーブルが解決できない場合（0 件として黙って通さない）。</exception>
        IReadOnlyList<TRecord> GetAll<TRecord>();

        /// <summary>
        /// 参照先テーブルに指定主キーのレコードが存在するか（外部キー検証用。O(1)）。
        /// </summary>
        /// <exception cref="InvalidOperationException">対象テーブルまたは主キーが解決できない場合。</exception>
        bool ContainsPrimaryKey(Type targetRecordType, int primaryKey);
    }
}
#endif
