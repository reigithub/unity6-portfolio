using System;
using Game.Shared.Scriptable.Database.Validation;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのプレイヤー武器（ハンドガン等）の調整値マスターデータ
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorWeaponMasterTable")]
    public partial class HorrorWeaponMaster
    {
        #region SerializeField

        [SerializeField] private int _id;
        [SerializeField] private string _developOnlyName; // 開発時のみの識別名
        [SerializeField] private string _name;
        [SerializeField] private string _description;
        [SerializeField] private string _iconAssetName;
        [SerializeField] private string _modelAssetName;
        [SerializeField] private int _maxCount;

        [SerializeField] private int _damage;              // 着弾ダメージ（IDamageable.TakeDamage へ渡す）
        [SerializeField] private float _range;             // 射程（Raycast 最大距離, m）
        [SerializeField] private float _fireInterval;      // 発砲後の硬直（AttackingState 滞在秒）
        [SerializeField] private float _noiseLoudness;     // 発砲音の大きさ（射手位置で発行、HorrorSignals.Noise.Occurred.Loudness）
        [SerializeField] private float _impactNoiseLoudness; // 着弾音の大きさ（着弾点で発行。誘引用）
        [SerializeField] private float _equipDuration;     // 装備切替の硬直（EquippingState 滞在秒）
        [SerializeField] private float _aimZoomRatio;      // エイム時 FOV 倍率（1=ズーム無し、小さいほどズーム）
        [SerializeField] private float _aimDamageMultiplier; // エイム射撃のダメージ倍率
        [SerializeField] private float _spreadAngle;       // 非エイム射撃のランダム拡散角（度）
        [SerializeField] private int _magazineSize;          // 弾倉容量
        [SerializeField] private float _reloadDuration;      // リロード硬直（ReloadingState 滞在秒）
        [SerializeField] private int _ammoItemId;            // 予備弾薬の HorrorItemMaster Id（0=弾薬概念なし・無限）
        [SerializeField] private string _dryFireSeAssetName; // 空撃ち SE アセット名（空文字=再生しない）
        [SerializeField] private string _fireSeAssetName;      // 射撃 SE アセット名（空文字=再生しない）
        [SerializeField] private string _muzzleFlashAssetName; // マズルフラッシュ VFX アセット名（空文字=表示しない）
        [SerializeField] private float _recoilCameraPitch;     // 発砲カメラリコイルの跳ね上げピッチ角（度）
        [SerializeField] private float _recoilRecoverSeconds;  // 発砲カメラリコイルが収まるまでの秒数（減衰オフセット型）

        // 詳細プレビューでモデルを提示する姿勢（Euler 度）。全て 0 でプレハブのオーサリング姿勢そのまま
        [SerializeField] private float _previewRotationX;
        [SerializeField] private float _previewRotationY;
        [SerializeField] private float _previewRotationZ;

        #endregion

        #region Database

        [PrimaryKey]
        public int Id
        {
            get => _id;
            set => _id = value;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public string Description
        {
            get => _description;
            set => _description = value;
        }

        public string IconAssetName
        {
            get => _iconAssetName;
            set => _iconAssetName = value;
        }

        public string ModelAssetName
        {
            get => _modelAssetName;
            set => _modelAssetName = value;
        }

        public int MaxCount
        {
            get => _maxCount;
            set => _maxCount = value;
        }

        public int Damage
        {
            get => _damage;
            set => _damage = value;
        }

        public float Range
        {
            get => _range;
            set => _range = value;
        }

        public float FireInterval
        {
            get => _fireInterval;
            set => _fireInterval = value;
        }

        public float NoiseLoudness
        {
            get => _noiseLoudness;
            set => _noiseLoudness = value;
        }

        public float ImpactNoiseLoudness
        {
            get => _impactNoiseLoudness;
            set => _impactNoiseLoudness = value;
        }

        public float EquipDuration
        {
            get => _equipDuration;
            set => _equipDuration = value;
        }

        public float AimZoomRatio
        {
            get => _aimZoomRatio;
            set => _aimZoomRatio = value;
        }

        public float AimDamageMultiplier
        {
            get => _aimDamageMultiplier;
            set => _aimDamageMultiplier = value;
        }

        public float SpreadAngle
        {
            get => _spreadAngle;
            set => _spreadAngle = value;
        }

        public int MagazineSize
        {
            get => _magazineSize;
            set => _magazineSize = value;
        }

        public float ReloadDuration
        {
            get => _reloadDuration;
            set => _reloadDuration = value;
        }

        [ForeignKey(typeof(HorrorItemMaster), AllowNone = true)]
        public int AmmoItemId
        {
            get => _ammoItemId;
            set => _ammoItemId = value;
        }

        public string DryFireSeAssetName
        {
            get => _dryFireSeAssetName;
            set => _dryFireSeAssetName = value;
        }

        public string FireSeAssetName
        {
            get => _fireSeAssetName;
            set => _fireSeAssetName = value;
        }

        public string MuzzleFlashAssetName
        {
            get => _muzzleFlashAssetName;
            set => _muzzleFlashAssetName = value;
        }

        public float RecoilCameraPitch
        {
            get => _recoilCameraPitch;
            set => _recoilCameraPitch = value;
        }

        public float RecoilRecoverSeconds
        {
            get => _recoilRecoverSeconds;
            set => _recoilRecoverSeconds = value;
        }

        public float PreviewRotationX
        {
            get => _previewRotationX;
            set => _previewRotationX = value;
        }

        public float PreviewRotationY
        {
            get => _previewRotationY;
            set => _previewRotationY = value;
        }

        public float PreviewRotationZ
        {
            get => _previewRotationZ;
            set => _previewRotationZ = value;
        }

        #endregion
    }
}
