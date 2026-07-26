using System;
using UnityEngine;

namespace Game.Shared.Scriptable.Database.Tables
{
    /// <summary>
    /// ホラーゲームのプレイヤー操作の調整値マスターデータ
    /// </summary>
    [Serializable]
    [ScriptableTable(Name = "Scriptable Database/Table/HorrorPlayerMasterTable")]
    public partial class HorrorPlayerMaster
    {
        #region SerializeField

        [SerializeField] private int _id;                       // 識別ID
        [SerializeField] private string _name;                  // 識別名
        [SerializeField] private string _modelAssetName;        // プレイヤーモデルの Addressables アドレス

        [SerializeField] private float _walkSpeed;              // 歩き速度（m/s）
        [SerializeField] private float _runSpeed;               // 走り速度（m/s）
        [SerializeField] private float _jump;                   // ジャンプ初速（m/s）
        [SerializeField] private float _gravity;                // 重力加速度（m/s^2, 負値）

        [SerializeField] private float _crouchSpeed;            // しゃがみ移動速度（m/s）
        [SerializeField] private float _crouchHeight;           // しゃがみ時の CharacterController 高さ（m）
        [SerializeField] private float _crouchTransitionSpeed;  // 立ち↔しゃがみ補間の応答速度（1-exp(-k・dt) の k）

        [SerializeField] private float _lookRotationSpeed;      // 視点回転速度（度/秒。オプション感度が乗算される基準値）
        [SerializeField] private float _aimTransitionSpeed;     // エイム構え補間の応答速度（1-exp(-k・dt) の k）
        [SerializeField] private float _aimRotationMultiplier;  // エイム中のカメラ回転速度倍率
        [SerializeField] private float _aimShakeFadeSeconds;    // エイム中にカメラ揺れをゼロへ減衰させる秒数（解除時の復帰も同じ秒数）

        [SerializeField] private float _headBobWalkAmplitude;       // ヘッドボブ歩き：縦位置振幅（m）
        [SerializeField] private float _headBobRunAmplitude;        // ヘッドボブ走り：縦位置振幅（m）
        [SerializeField] private float _headBobWalkSpeed;           // ヘッドボブ歩き：位相速度 rad/s（ゆっくり）
        [SerializeField] private float _headBobRunSpeed;            // ヘッドボブ走り：位相速度 rad/s（少しだけ速い）
        [SerializeField] private float _headBobHorizontalRatio;     // ヘッドボブ横位置/縦位置 比
        [SerializeField] private float _headBobWalkRoll;            // ヘッドボブ歩き：ロール角（度）＝知覚される横揺れ
        [SerializeField] private float _headBobRunRoll;             // ヘッドボブ走り：ロール角（度）
        [SerializeField] private float _headBobAmplitudeResponse;   // ヘッドボブ強度イーズの応答

        [SerializeField] private float _idleSwaySpeed;          // アイドルスウェイ：位相速度 rad/s（呼吸 ~5秒周期）
        [SerializeField] private float _idleSwayAmplitude;      // アイドルスウェイ：縦位置振幅（m, ヘッドボブより小）
        [SerializeField] private float _idleSwayRoll;           // アイドルスウェイ：ロール角（度, 小）

        [SerializeField] private float _footstepStride;         // 足音の歩幅（m）。この距離を移動するたびに1歩発火
        [SerializeField] private float _footstepWalkLoudness;   // 歩き足音の大きさ（HorrorSignals.Noise.Occurred.Loudness）
        [SerializeField] private float _footstepRunLoudness;    // 走り足音の大きさ（同上）
        [SerializeField] private string _footstepSeAssetName;   // 足音 SE アセット名（空文字=再生しない）

        [SerializeField] private int _maxHealth;                // 最大 HP
        [SerializeField] private float _invincibleSeconds;      // 被弾後の無敵時間（秒）
        [SerializeField] private string _damageSeAssetName;     // 被弾 SE アセット名（空文字=再生しない）

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

        public string ModelAssetName
        {
            get => _modelAssetName;
            set => _modelAssetName = value;
        }

        public float WalkSpeed
        {
            get => _walkSpeed;
            set => _walkSpeed = value;
        }

        public float RunSpeed
        {
            get => _runSpeed;
            set => _runSpeed = value;
        }

        public float Jump
        {
            get => _jump;
            set => _jump = value;
        }

        public float Gravity
        {
            get => _gravity;
            set => _gravity = value;
        }

        public float CrouchSpeed
        {
            get => _crouchSpeed;
            set => _crouchSpeed = value;
        }

        public float CrouchHeight
        {
            get => _crouchHeight;
            set => _crouchHeight = value;
        }

        public float CrouchTransitionSpeed
        {
            get => _crouchTransitionSpeed;
            set => _crouchTransitionSpeed = value;
        }

        public float LookRotationSpeed
        {
            get => _lookRotationSpeed;
            set => _lookRotationSpeed = value;
        }

        public float AimTransitionSpeed
        {
            get => _aimTransitionSpeed;
            set => _aimTransitionSpeed = value;
        }

        public float AimRotationMultiplier
        {
            get => _aimRotationMultiplier;
            set => _aimRotationMultiplier = value;
        }

        public float AimShakeFadeSeconds
        {
            get => _aimShakeFadeSeconds;
            set => _aimShakeFadeSeconds = value;
        }

        public float HeadBobWalkAmplitude
        {
            get => _headBobWalkAmplitude;
            set => _headBobWalkAmplitude = value;
        }

        public float HeadBobRunAmplitude
        {
            get => _headBobRunAmplitude;
            set => _headBobRunAmplitude = value;
        }

        public float HeadBobWalkSpeed
        {
            get => _headBobWalkSpeed;
            set => _headBobWalkSpeed = value;
        }

        public float HeadBobRunSpeed
        {
            get => _headBobRunSpeed;
            set => _headBobRunSpeed = value;
        }

        public float HeadBobHorizontalRatio
        {
            get => _headBobHorizontalRatio;
            set => _headBobHorizontalRatio = value;
        }

        public float HeadBobWalkRoll
        {
            get => _headBobWalkRoll;
            set => _headBobWalkRoll = value;
        }

        public float HeadBobRunRoll
        {
            get => _headBobRunRoll;
            set => _headBobRunRoll = value;
        }

        public float HeadBobAmplitudeResponse
        {
            get => _headBobAmplitudeResponse;
            set => _headBobAmplitudeResponse = value;
        }

        public float IdleSwaySpeed
        {
            get => _idleSwaySpeed;
            set => _idleSwaySpeed = value;
        }

        public float IdleSwayAmplitude
        {
            get => _idleSwayAmplitude;
            set => _idleSwayAmplitude = value;
        }

        public float IdleSwayRoll
        {
            get => _idleSwayRoll;
            set => _idleSwayRoll = value;
        }

        public float FootstepStride
        {
            get => _footstepStride;
            set => _footstepStride = value;
        }

        public float FootstepWalkLoudness
        {
            get => _footstepWalkLoudness;
            set => _footstepWalkLoudness = value;
        }

        public float FootstepRunLoudness
        {
            get => _footstepRunLoudness;
            set => _footstepRunLoudness = value;
        }

        public string FootstepSeAssetName
        {
            get => _footstepSeAssetName;
            set => _footstepSeAssetName = value;
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public float InvincibleSeconds
        {
            get => _invincibleSeconds;
            set => _invincibleSeconds = value;
        }

        public string DamageSeAssetName
        {
            get => _damageSeAssetName;
            set => _damageSeAssetName = value;
        }

        #endregion
    }
}
