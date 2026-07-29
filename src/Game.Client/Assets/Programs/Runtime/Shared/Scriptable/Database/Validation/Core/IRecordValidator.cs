#if UNITY_EDITOR
namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// レコード 1 件ごとの検証。実装クラスは引数なしコンストラクタを持てば
    /// <see cref="ValidationExecutor"/> が自動発見して対象テーブルへ登録する。
    /// </summary>
    public interface IRecordValidator<TRecord>
    {
        void Validate(TRecord record, ValidationResult result, IRecordGetter recordGetter);
    }
}
#endif
