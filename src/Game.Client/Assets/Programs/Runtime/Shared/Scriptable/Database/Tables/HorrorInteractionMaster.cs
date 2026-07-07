using System;
using Game.Shared.Enums;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// Horror のフィールドインタラクト定義。対象（<see cref="IInteractable"/> 実装）が Id で参照し、
    /// 入力方式・実行条件・効果パラメータをデータから引く。動詞と効果メカニクスはコード（具象）側に残す。
    /// （Unity が List 要素としてシリアライズできるよう [Serializable]）
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorInteractionMasterTable")]
    public partial class HorrorInteractionMaster
    {
        #region SerializeField

        [SerializeField] private int _id;
        [SerializeField] private string _name;

        [SerializeField] private InteractionInputType _inputType;
        [SerializeField] private float _holdSeconds;

        [SerializeField] private string _interactionVerbLocalizeKey;
        [SerializeField] private string _reinteractionVerbLocalizeKey;

        [SerializeField] private string _rejectionMessageLocalizeKey;

        [SerializeField] private int _requiredItemId;

        [SerializeField] private int _acquiredId;
        [SerializeField] private int _acquiredCount;

        [SerializeField] private bool _checkpointSave;

        #endregion

        #region Columns

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

        /// <summary>起動方式（単発／長押し／トグル）。</summary>
        public InteractionInputType InputType
        {
            get => _inputType;
            set => _inputType = value;
        }

        /// <summary>Hold 時の長押し秒数。</summary>
        public float HoldSeconds
        {
            get => _holdSeconds;
            set => _holdSeconds = value;
        }

        public string InteractionVerbLocalizeKey
        {
            get => _interactionVerbLocalizeKey;
            set => _interactionVerbLocalizeKey = value;
        }

        public string ReinteractionVerbLocalizeKey
        {
            get => _reinteractionVerbLocalizeKey;
            set => _reinteractionVerbLocalizeKey = value;
        }

        public string RejectionMessageLocalizeKey
        {
            get => _rejectionMessageLocalizeKey;
            set => _rejectionMessageLocalizeKey = value;
        }

        /// <summary>実行に必要なアイテム Id（鍵など）。0 は無条件。</summary>
        public int RequiredItemId
        {
            get => _requiredItemId;
            set => _requiredItemId = value;
        }

        /// <summary>効果として付与するアイテム Id。0 はなし。</summary>
        public int AcquiredId
        {
            get => _acquiredId;
            set => _acquiredId = value;
        }

        /// <summary>付与数量。</summary>
        public int AcquiredCount
        {
            get => _acquiredCount;
            set => _acquiredCount = value;
        }

        public bool CheckpointSave
        {
            get => _checkpointSave;
            set => _checkpointSave = value;
        }

        #endregion
    }
}
