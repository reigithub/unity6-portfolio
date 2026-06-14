using System;

namespace Game.Shared.Scriptable.Database
{
    /// <summary>テーブル化するマスターデータクラスに付与する。生成テーブルの CreateAssetMenu 名に使う。</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ScriptableTableAttribute : Attribute
    {
        public string Name { get; set; }
    }

    /// <summary>int 主キー。1 つのクラスに 1 つ。</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class PrimaryKeyAttribute : Attribute
    {
    }

    /// <summary>
    /// 二次キー。同じ <see cref="IndexNo"/> を複数プロパティに付与すると複合キー（<see cref="KeyOrder"/> で列順）。
    /// AllowMultiple のため 1 プロパティが複数インデックスに参加できる。
    /// 非ユニーク性はこの属性の <see cref="NonUnique"/> で **index ごとに** 指定する
    /// （プロパティ単位の別属性にすると複数 index に波及するため。リフレクションは角括弧グループを区別できない）。
    /// 複合キーは構成列のいずれかが <see cref="NonUnique"/>=true なら index 全体が非ユニーク（OR 集約）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true)]
    public sealed class SecondaryKeyAttribute : Attribute
    {
        public int IndexNo { get; }
        public int KeyOrder { get; }

        /// <summary>この index が重複値を許す（多件ヒット）か。無指定は一意。</summary>
        public bool NonUnique { get; }

        public SecondaryKeyAttribute(int indexNo, int keyOrder = 0, bool nonUnique = false)
        {
            IndexNo = indexNo;
            KeyOrder = keyOrder;
            NonUnique = nonUnique;
        }
    }
}
