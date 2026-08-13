#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Game.Editor.Addressables
{
    /// <summary>
    /// Horror ドロップ品のモデルアセットを HorrorItems Addressable グループに登録し、
    /// 旧方式の一体化ドロップ品プレハブの登録を解除するユーティリティ。
    /// ドロップ品は共通プレハブ（HorrorDropItem）にモデルを実行時装着する方式のため、
    /// ここで登録するのは素のモデルアセットで、アドレスは HorrorItemMaster.ModelAssetName と一致させる。
    /// アドレスはファイル名から機械的に導けない（Herbal Plant 1 green は末尾の数字を残すが
    /// Medical First Aid Bottle 1 は落とす）ため、明示的な対応表で持つ。
    /// </summary>
    public static class RegisterHorrorItemAddressables
    {
        private const string GroupName = "HorrorItems";

        private const string HerbalMedicPack = "Assets/StoreAssets/Hash Game studios/Herbal & Medic Pack/Prefabs/";
        private const string LowPolyWeapons = "Assets/StoreAssets/Low Poly Weapons VOL.1/Prefabs/";
        private const string LegacyDropItems = "Assets/ProjectAssets/Horror/DropItem/";

        // マスターデータ（HorrorItemMaster.ModelAssetName）が参照するアドレスは変更不可。
        // それ以外はファイル名からスペースを除去し、バリアント名の前にアンダースコアを入れる規則で命名する。
        private static readonly (string path, string address)[] Models =
        {
            (HerbalMedicPack + "Herbal Plant 1/Herbal Plant 1 blue.prefab", "HerbalPlant1_blue"),
            (HerbalMedicPack + "Herbal Plant 1/Herbal Plant 1 custom.prefab", "HerbalPlant1_custom"),
            (HerbalMedicPack + "Herbal Plant 1/Herbal Plant 1 gold.prefab", "HerbalPlant1_gold"),
            (HerbalMedicPack + "Herbal Plant 1/Herbal Plant 1 green.prefab", "HerbalPlant1_green"),
            (HerbalMedicPack + "Herbal Plant 1/Herbal Plant 1 red.prefab", "HerbalPlant1_red"),

            (HerbalMedicPack + "Medic pack/1/Medic pack 1 custom 1.prefab", "MedicPack1_custom1"),
            (HerbalMedicPack + "Medic pack/1/Medic pack 1 custom 2.prefab", "MedicPack1_custom2"),
            (HerbalMedicPack + "Medic pack/2/Medic pack 2 custom 1.prefab", "MedicPack2_custom1"),
            (HerbalMedicPack + "Medic pack/2/Medic pack 2 custom 2.prefab", "MedicPack2_custom2"),
            (HerbalMedicPack + "Medic pack/2/Medic pack 2_collider.prefab", "MedicPack2_collider"),
            (HerbalMedicPack + "Medic pack/Medic pack 3.prefab", "MedicPack3"),

            (HerbalMedicPack + "Medic pack/Medical First Aid Bottle 1/Medical First Aid Bottle 1.prefab", "MedicalFirstAidBottle"),
            (HerbalMedicPack + "Medic pack/Medical First Aid Bottle 1/Medical First Aid Bottle 1_collider.prefab", "MedicalFirstAidBottle_collider"),

            (HerbalMedicPack + "Medic pack/Metal Syringe 1/Metal Syringe 1 blue.prefab", "MetalSyringe1_blue"),
            (HerbalMedicPack + "Medic pack/Metal Syringe 1/Metal Syringe 1 green.prefab", "MetalSyringe1_green"),
            (HerbalMedicPack + "Medic pack/Metal Syringe 1/Metal Syringe 1 orange.prefab", "MetalSyringe1_orange"),
            (HerbalMedicPack + "Medic pack/Metal Syringe 1/Metal Syringe 1 red.prefab", "MetalSyringe1_red"),

            (HerbalMedicPack + "Medic pack/Vial Vaccine/Vial Vaccine Blue.prefab", "VialVaccine_Blue"),
            (HerbalMedicPack + "Medic pack/Vial Vaccine/Vial Vaccine Green.prefab", "VialVaccine_Green"),
            (HerbalMedicPack + "Medic pack/Vial Vaccine/Vial Vaccine Orange.prefab", "VialVaccine_Orange"),
            (HerbalMedicPack + "Medic pack/Vial Vaccine/Vial Vaccine Red.prefab", "VialVaccine_Red"),
            (HerbalMedicPack + "Medic pack/Vial Vaccine/Vial Vaccine_collider.prefab", "VialVaccine_collider"),

            (HerbalMedicPack + "Recovery spray 1/Recovery spray 1.prefab", "RecoverySpray1"),
            (HerbalMedicPack + "Recovery spray 1/Recovery spray 1 custom 1.prefab", "RecoverySpray1_custom1"),
            (HerbalMedicPack + "Recovery spray 1/Recovery spray 1 custom 2.prefab", "RecoverySpray1_custom2"),
            (HerbalMedicPack + "Recovery spray 1/Recovery spray 1_collider.prefab", "RecoverySpray1_collider"),

            (HerbalMedicPack + "Render Room/Room 1.prefab", "Room1"),
            (HerbalMedicPack + "Render Room/Room 2.prefab", "Room2"),

            (LowPolyWeapons + "M1911_Magazin.prefab", "M1911_Magazin"),
        };

        // 旧方式の一体化プレハブ。共通プレハブ方式への移行で役割を終えたため登録を解除する
        private static readonly string[] LegacyEntries =
        {
            LegacyDropItems + "HerbalPlant1_blue.prefab",
            LegacyDropItems + "HerbalPlant1_gold.prefab",
            LegacyDropItems + "HerbalPlant1_green.prefab",
            LegacyDropItems + "HerbalPlant1_red.prefab",
            LegacyDropItems + "M1911_Magazin.prefab",
            LegacyDropItems + "MedicalFirstAidBottle.prefab",
            LegacyDropItems + "VialVaccine_Green.prefab",
        };

        [MenuItem("Tools/Addressables/Register Horror Item Models")]
        public static void Execute()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(RegisterHorrorItemAddressables)}] AddressableAssetSettings not found");
                return;
            }

            var group = settings.FindGroup(GroupName);
            if (group == null)
            {
                Debug.LogError($"[{nameof(RegisterHorrorItemAddressables)}] Group '{GroupName}' not found");
                return;
            }

            // 解除を先に行う。旧エントリと同じアドレスを新エントリへ付け替えるため、
            // 逆順だと同一アドレスが一時的に重複する
            var removed = 0;
            foreach (var path in LegacyEntries)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    // 既に削除済みのアセットは解除対象としても存在しないため、警告に留める
                    Debug.LogWarning($"[{nameof(RegisterHorrorItemAddressables)}] Asset not found (skip unregister): {path}");
                    continue;
                }

                if (settings.RemoveAssetEntry(guid, postEvent: false))
                {
                    removed++;
                    Debug.Log($"[{nameof(RegisterHorrorItemAddressables)}] Unregistered: {path}");
                }
            }

            var registered = 0;
            foreach (var (path, address) in Models)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    Debug.LogError($"[{nameof(RegisterHorrorItemAddressables)}] Asset not found: {path}");
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                entry.address = address;
                registered++;
                Debug.Log($"[{nameof(RegisterHorrorItemAddressables)}] Registered: {address} → {path}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(RegisterHorrorItemAddressables)}] Done. registered={registered}/{Models.Length}, unregistered={removed}/{LegacyEntries.Length}");
        }
    }
}
#endif
