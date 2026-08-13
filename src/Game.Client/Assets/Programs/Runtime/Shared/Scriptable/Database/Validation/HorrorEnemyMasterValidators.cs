#if UNITY_EDITOR
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 撃破時ドロップの抽選グループ（DropGroupId ≠ 0）に対応する抽選行が存在することを検証する。
    /// DropGroupId はグループキー（参照先の主キーではない）のため <see cref="ForeignKeyAttribute"/> では宣言できない。
    /// </summary>
    public sealed class HorrorEnemyMasterDropGroupValidator : IRecordValidator<HorrorEnemyMaster>
    {
        public void Validate(HorrorEnemyMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            if (record.DropGroupId == 0) return; // 0 = ドロップなし（ForeignKey の AllowNone 相当）

            foreach (var drop in recordGetter.GetAll<HorrorEnemyDropMaster>())
            {
                if (drop is null) continue;

                if (drop.DropGroupId == record.DropGroupId) return;
            }

            result.AddError(record.Id.ToString(),
                $"DropGroupId={record.DropGroupId} を持つ {nameof(HorrorEnemyDropMaster)} の行がありません。");
        }
    }
}
#endif
