using System;
using System.Collections.Generic;
using Game.Horror.Database;
using Game.Horror.Inventory;
using Game.Horror.Services.Interfaces;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// クラフト（素材を消費して成果物を得る合成）を扱うドメインサービス。
    /// レシピと素材はマスターデータが持ち、所持数の判定と増減はインベントリサービスへ委譲する。
    /// 素材だけ消えて成果物が入らない事態を防ぐため、消費前に
    /// <see cref="IHorrorInventoryService.CanAddAfterConsume"/> で消費後の空きまで含めて判定する。
    /// </summary>
    public class HorrorCraftService : IHorrorCraftService
    {
        private static readonly HorrorCraftMaterialMaster[] _emptyMaterials = Array.Empty<HorrorCraftMaterialMaster>();

        private readonly IScriptableDatabaseService _databaseService;
        private readonly IHorrorInventoryService _inventoryService;

        // 素材の数量指定はインベントリ判定のたびに必要になる（長押し中の実行可否判定を含む）。
        // 呼び出しごとの確保を避けるため使い回す。
        private readonly List<HorrorObjectAmount> _materialAmounts = new();

        public HorrorCraftService(IScriptableDatabaseService databaseService, IHorrorInventoryService inventoryService)
        {
            _databaseService = databaseService;
            _inventoryService = inventoryService;
        }

        /// <summary>全レシピ（解放条件は持たず、素材不足のレシピも含む）。</summary>
        public IReadOnlyList<HorrorCraftMaster> Recipes => _databaseService.Database.HorrorCraftMasterTable.All;

        /// <summary>レシピが要求する素材一覧。未知のレシピは空。</summary>
        public IReadOnlyList<HorrorCraftMaterialMaster> GetMaterials(int craftId)
        {
            if (!_databaseService.Database.HorrorCraftMasterTable.TryFindById(craftId, out var recipe))
                return _emptyMaterials;

            return _databaseService.Database.HorrorCraftMaterialMasterTable.FindByMaterialGroupId(recipe.MaterialGroupId);
        }

        /// <summary>
        /// 実行可能か（素材が足りていて、消費後の空きに成果物が全量入るか）を判定する。インベントリは変更しない。
        /// </summary>
        public bool CanCraft(int craftId)
        {
            if (!TryPrepare(craftId, out var recipe, out var resultMaxCount))
                return false;

            return _inventoryService.CanAddAfterConsume(_materialAmounts, recipe.ResultObjectCategory, recipe.ResultObjectId, recipe.ResultCount, resultMaxCount);
        }

        /// <summary>
        /// クラフトを実行する。<see cref="CanCraft"/> が false のときは何もせず false（部分消費しない）。
        /// </summary>
        public bool TryCraft(int craftId)
        {
            if (!TryPrepare(craftId, out var recipe, out var resultMaxCount))
                return false;

            if (!_inventoryService.CanAddAfterConsume(_materialAmounts, recipe.ResultObjectCategory, recipe.ResultObjectId, recipe.ResultCount, resultMaxCount))
            {
                return false;
            }

            // ここから先の失敗は事前判定との齟齬（不変条件違反）。素材を消したまま成果物が入らない状態になるため顕在化させる
            foreach (var amount in _materialAmounts)
            {
                if (!_inventoryService.TryConsume(amount.Category, amount.Id, amount.Count))
                {
                    Debug.LogError(
                        $"[{nameof(HorrorCraftService)}] 事前判定を通過した素材の消費に失敗しました: " +
                        $"CraftId={craftId}, ({amount.Category}, {amount.Id}) x{amount.Count}");
                    return false;
                }
            }

            if (!_inventoryService.TryAdd(recipe.ResultObjectCategory, recipe.ResultObjectId, recipe.ResultCount, resultMaxCount))
            {
                Debug.LogError(
                    $"[{nameof(HorrorCraftService)}] 事前判定を通過した成果物の付与に失敗しました: " +
                    $"CraftId={craftId}, ({recipe.ResultObjectCategory}, {recipe.ResultObjectId}) x{recipe.ResultCount}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// レシピと成果物のスタック上限を解決し、素材の数量指定（<see cref="_materialAmounts"/>）を組み立てる。
        /// レシピ・成果物が引けない、素材が 1 件も無いレシピは実行不可として false
        /// （素材 0 件は無条件クラフトになるためデータ側でも検証している）。
        /// </summary>
        private bool TryPrepare(int craftId, out HorrorCraftMaster recipe, out int resultMaxCount)
        {
            resultMaxCount = 0;
            var database = _databaseService.Database;

            if (!database.HorrorCraftMasterTable.TryFindById(craftId, out recipe))
                return false;

            if (!HorrorDatabaseHelper.TryGetInfo(database, recipe.ResultObjectCategory, recipe.ResultObjectId, out var resultInfo))
            {
                return false;
            }

            resultMaxCount = resultInfo.MaxCount;

            _materialAmounts.Clear();
            foreach (var material in database.HorrorCraftMaterialMasterTable.FindByMaterialGroupId(recipe.MaterialGroupId))
            {
                _materialAmounts.Add(new HorrorObjectAmount
                {
                    Category = material.ObjectCategory,
                    Id = material.ObjectId,
                    Count = material.Count
                });
            }

            return _materialAmounts.Count > 0;
        }
    }
}
