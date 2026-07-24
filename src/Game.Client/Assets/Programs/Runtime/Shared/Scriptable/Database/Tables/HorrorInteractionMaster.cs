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
        [SerializeField] private string _developOnlyName; // 開発時のみの識別名
        [SerializeField] private string _name;

        [SerializeField] private InteractionInputType _inputType;
        [SerializeField] private float _holdSeconds;

        [SerializeField] private string _interactionVerb;
        [SerializeField] private string _reinteractionVerb;

        [SerializeField] private ObjectCategory _requiredObjectCategory;
        [SerializeField] private int _requiredObjectId;
        [SerializeField] private string _rejectionMessage;

        [SerializeField] private int _acquiredId;
        [SerializeField] private int _acquiredCount;

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

        public string InteractionVerb
        {
            get => _interactionVerb;
            set => _interactionVerb = value;
        }

        public string ReinteractionVerb
        {
            get => _reinteractionVerb;
            set => _reinteractionVerb = value;
        }

        public ObjectCategory RequiredObjectCategory
        {
            get => _requiredObjectCategory;
            set => _requiredObjectCategory = value;
        }

        /// <summary>実行に必要なアイテム Id（鍵など）。0 は無条件。</summary>
        public int RequiredObjectId
        {
            get => _requiredObjectId;
            set => _requiredObjectId = value;
        }

        public string RejectionMessage
        {
            get => _rejectionMessage;
            set => _rejectionMessage = value;
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

        #endregion
    }
}
