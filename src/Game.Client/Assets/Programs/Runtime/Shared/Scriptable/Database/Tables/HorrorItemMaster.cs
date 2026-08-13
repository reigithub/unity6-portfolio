using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorItemMasterTable")]
    public partial class HorrorItemMaster
    {
        #region SerializeField

        [SerializeField] private int _id;
        [SerializeField] private string _developOnlyName; // 開発時のみの識別名
        [SerializeField] private string _name;
        [SerializeField] private string _description;
        [SerializeField] private string _iconAssetName;
        [SerializeField] private string _modelAssetName;     // モデルプレハブの Addressables アドレス（空はドロップ非対応）
        [SerializeField] private int _maxCount;
        [SerializeField] private int _effect;
        [SerializeField] private float _effectDuration;      // 効果持続時間
        [SerializeField] private float _effectApplyDuration; // 効果適用にかかる時間
        [SerializeField] private bool _keyItem;

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

        public int Effect
        {
            get => _effect;
            set => _effect = value;
        }

        public float EffectDuration
        {
            get => _effectDuration;
            set => _effectDuration = value;
        }

        public float EffectApplyDuration
        {
            get => _effectApplyDuration;
            set => _effectApplyDuration = value;
        }

        public bool KeyItem
        {
            get => _keyItem;
            set => _keyItem = value;
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
