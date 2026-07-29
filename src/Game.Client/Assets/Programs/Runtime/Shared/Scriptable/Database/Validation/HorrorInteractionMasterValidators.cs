#if UNITY_EDITOR
using System;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 実行条件（RequiredObjectCategory / RequiredObjectId）の整合を検証する。
    /// 参照先テーブルがカテゴリによって変わるため [ForeignKey] では表せず、C# で分岐を書いている。
    /// </summary>
    public sealed class HorrorInteractionMasterValidator : IRecordValidator<HorrorInteractionMaster>
    {
        public void Validate(HorrorInteractionMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            string key = record.Id.ToString();

            #region RequiredObject

            // 実行条件の解釈は InteractableBase と合わせる：カテゴリ未指定または Id=0 は「条件なし」。
            if (record.RequiredObjectCategory == ObjectCategory.None)
            {
                if (record.RequiredObjectId != 0)
                {
                    result.AddError(key,
                        $"RequiredObjectCategory が None のため RequiredObjectId={record.RequiredObjectId} は無視されます。");
                }

                return;
            }

            if (record.RequiredObjectId == 0) return;

            switch (record.RequiredObjectCategory)
            {
                case ObjectCategory.Item:
                    RequirePrimaryKey(typeof(HorrorItemMaster), record, result, recordGetter);
                    break;
                case ObjectCategory.Weapon:
                    RequirePrimaryKey(typeof(HorrorWeaponMaster), record, result, recordGetter);
                    break;
                default:
                    result.AddError(key, $"RequiredObjectCategory={record.RequiredObjectCategory} に対応する参照先テーブルがありません。");
                    break;
            }

            #endregion
        }

        private static void RequirePrimaryKey(Type target, HorrorInteractionMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            if (recordGetter.ContainsPrimaryKey(target, record.RequiredObjectId)) return;

            result.AddError(record.Id.ToString(),
                $"RequiredObjectId={record.RequiredObjectId} に対応する {target.Name} のレコードがありません。");
        }
    }
}
#endif
