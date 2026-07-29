using System;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 参照整合性（外部キー）。この列の値は <see cref="TargetRecordType"/> の
    /// <see cref="PrimaryKeyAttribute"/> 列に存在しなければならない。
    /// 検証の実行は編集時のみ（Validation は UNITY_EDITOR 限定）だが、宣言はレコード定義の一部として常に残す。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ForeignKeyAttribute : Attribute
    {
        /// <summary>参照先のレコード型。</summary>
        public Type TargetRecordType { get; }

        /// <summary>0 を「未設定」として検証対象外にする。無指定なら 0 も存在チェックの対象。</summary>
        public bool AllowNone { get; set; }

        public ForeignKeyAttribute(Type targetRecordType)
        {
            TargetRecordType = targetRecordType;
        }
    }

    /// <summary>
    /// 文字列に値が入っていることを要求する（string 専用）。null は常にエラー、空文字は <see cref="AllowEmpty"/> 次第。
    /// 「空文字＝機能を使わない」が正当な列（SE アセット名など）には付けない。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class StringNotNullAttribute : Attribute
    {
        /// <summary>空文字を許す（null のみエラーにする）。無指定なら空文字もエラー。</summary>
        public bool AllowEmpty { get; set; }
    }

    /// <summary>
    /// 数値の許容範囲（両端を含む）。対象は int / long / float / double
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class ValueRangeAttribute : Attribute
    {
        public double Minimum { get; }
        public double Maximum { get; }

        public ValueRangeAttribute(double minimum, double maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
    }

    /// <summary>文字列長の許容範囲（両端を含む、string 専用）。null は検証しない（<see cref="StringNotNullAttribute"/> の担当）。</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class StringLengthAttribute : Attribute
    {
        public int MaximumLength { get; }

        /// <summary>最小長。無指定なら 0（下限なし）。</summary>
        public int MinimumLength { get; set; }

        public StringLengthAttribute(int maximumLength)
        {
            MaximumLength = maximumLength;
        }
    }

    /// <summary>文字列の書式（string 専用）。null は検証しない（<see cref="StringNotNullAttribute"/> の担当）。</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class RegularExpressionAttribute : Attribute
    {
        public string Pattern { get; }

        public RegularExpressionAttribute(string pattern)
        {
            Pattern = pattern;
        }
    }

    /// <summary>比較演算子。<see cref="CompareAttribute"/> で使う。</summary>
    public enum CompareOperator
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    /// <summary>
    /// 同じレコード内の別メンバとの大小関係（閾値の順序など）。両メンバは同じ型で、比較可能である必要がある。
    /// 演算子は既定値を持たせない（付け忘れが黙って等値比較になるのを避けるため）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class CompareAttribute : Attribute
    {
        /// <summary>比較相手のメンバ名。<c>nameof</c> で指定する。</summary>
        public string OtherMember { get; }

        /// <summary>この列が相手に対して満たすべき関係。</summary>
        public CompareOperator Operator { get; }

        public CompareAttribute(string otherMember, CompareOperator op)
        {
            OtherMember = otherMember;
            Operator = op;
        }
    }

    /// <summary>
    /// テーブル内で値が重複しないことを要求する。
    /// 索引（<see cref="PrimaryKeyAttribute"/> / <see cref="SecondaryKeyAttribute"/>）とは独立した宣言で、
    /// 索引を張らない列にも付けられる。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class UniqueAttribute : Attribute
    {
    }
}
