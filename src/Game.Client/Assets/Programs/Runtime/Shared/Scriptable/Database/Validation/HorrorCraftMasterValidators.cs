#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Game.Shared.Enums;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// (ObjectCategory, Id) の組で対象を指す列の参照先レコード型を解決する。
    /// <see cref="ForeignKeyAttribute"/> は参照先が単一テーブル固定のため、種別で参照先が変わる列は
    /// この解決を通して validator 側で存在確認する。
    /// </summary>
    internal static class HorrorObjectMasterTypes
    {
        public static bool TryResolve(ObjectCategory category, out Type recordType)
        {
            switch (category)
            {
                case ObjectCategory.Item:
                    recordType = typeof(HorrorItemMaster);
                    return true;
                case ObjectCategory.Weapon:
                    recordType = typeof(HorrorWeaponMaster);
                    return true;
                default:
                    recordType = null;
                    return false;
            }
        }
    }

    /// <summary>
    /// 生成物の指定が実在することを検証する。種別が未設定（None）だと参照先テーブルを決められず、
    /// Id が実在しないとクラフト成功時に付与できないためどちらもデータミス。
    /// </summary>
    public sealed class HorrorCraftResultValidator : IRecordValidator<HorrorCraftMaster>
    {
        public void Validate(HorrorCraftMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            if (!HorrorObjectMasterTypes.TryResolve(record.ResultObjectCategory, out var recordType))
            {
                result.AddError(record.Id.ToString(),
                    $"{nameof(HorrorCraftMaster.ResultObjectCategory)}={record.ResultObjectCategory} は生成物の種別として指定できません。");
                return;
            }

            if (!recordGetter.ContainsPrimaryKey(recordType, record.ResultObjectId))
            {
                result.AddError(record.Id.ToString(),
                    $"{nameof(HorrorCraftMaster.ResultObjectId)}={record.ResultObjectId} は {recordType.Name} に存在しません。");
            }
        }
    }

    /// <summary>
    /// レシピが素材グループを持つことを検証する。素材 0 件のレシピは無条件クラフトになってしまうため、
    /// グループの実体はレコード単位では判定できず <see cref="IRecordsValidator{TRecord}"/> で書く。
    /// </summary>
    public sealed class HorrorCraftMaterialGroupValidator : IRecordsValidator<HorrorCraftMaster>
    {
        public void Validate(
            IReadOnlyList<HorrorCraftMaster> allRecords, ValidationResult result, IRecordGetter recordGetter)
        {
            var materials = recordGetter.GetAll<HorrorCraftMaterialMaster>();

            var definedGroups = new HashSet<int>();
            for (int i = 0; i < materials.Count; i++)
            {
                var material = materials[i];
                if (material is null) continue;

                definedGroups.Add(material.MaterialGroupId);
            }

            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                if (record is null) continue;

                if (!definedGroups.Contains(record.MaterialGroupId))
                {
                    result.AddError(record.Id.ToString(),
                        $"{nameof(HorrorCraftMaster.MaterialGroupId)}={record.MaterialGroupId} に対応する " +
                        $"{nameof(HorrorCraftMaterialMaster)} の行がありません。");
                }
            }
        }
    }

    /// <summary>
    /// 素材の指定が実在することを検証する。生成物と同じく種別で参照先が変わるため宣言属性では書けない。
    /// </summary>
    public sealed class HorrorCraftMaterialObjectValidator : IRecordValidator<HorrorCraftMaterialMaster>
    {
        public void Validate(HorrorCraftMaterialMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            if (!HorrorObjectMasterTypes.TryResolve(record.ObjectCategory, out var recordType))
            {
                result.AddError(record.Id.ToString(),
                    $"{nameof(HorrorCraftMaterialMaster.ObjectCategory)}={record.ObjectCategory} は素材の種別として指定できません。");
                return;
            }

            if (!recordGetter.ContainsPrimaryKey(recordType, record.ObjectId))
            {
                result.AddError(record.Id.ToString(),
                    $"{nameof(HorrorCraftMaterialMaster.ObjectId)}={record.ObjectId} は {recordType.Name} に存在しません。");
            }
        }
    }

    /// <summary>
    /// 同一グループ内で同じ素材が複数行に分かれていないことを検証する。
    /// 分かれていると 1 素材あたりの必要数がどの行なのか決まらず、消費数量の意味が壊れる（1 行にまとめること）。
    /// </summary>
    public sealed class HorrorCraftMaterialDuplicateValidator : IRecordsValidator<HorrorCraftMaterialMaster>
    {
        public void Validate(
            IReadOnlyList<HorrorCraftMaterialMaster> allRecords, ValidationResult result, IRecordGetter recordGetter)
        {
            var seen = new HashSet<(int groupId, ObjectCategory category, int objectId)>();
            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                if (record is null) continue;

                var key = (record.MaterialGroupId, record.ObjectCategory, record.ObjectId);
                if (!seen.Add(key))
                {
                    result.AddError(record.Id.ToString(),
                        $"{nameof(HorrorCraftMaterialMaster.MaterialGroupId)}={record.MaterialGroupId} に " +
                        $"({record.ObjectCategory}, {record.ObjectId}) が重複しています。1 行にまとめてください。");
                }
            }
        }
    }
}
#endif
