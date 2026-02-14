using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.MVP.Survivor.ECS
{
    /// <summary>
    /// 敵スポーン要求（1フレームで消費される一時エンティティ）
    /// </summary>
    public struct EnemySpawnRequest : IComponentData
    {
        /// <summary>敵マスターID</summary>
        public int EnemyId;

        /// <summary>スポーン位置</summary>
        public float3 Position;

        /// <summary>最大HP（倍率適用済み）</summary>
        public int MaxHp;

        /// <summary>攻撃力（倍率適用済み）</summary>
        public int AttackDamage;

        /// <summary>移動速度（倍率適用済み）</summary>
        public float MoveSpeed;

        /// <summary>攻撃範囲</summary>
        public float AttackRange;

        /// <summary>攻撃クールダウン（秒）</summary>
        public float AttackCooldown;

        /// <summary>ヒットスタン時間（秒）</summary>
        public float HitStunDuration;

        /// <summary>回転速度</summary>
        public float RotationSpeed;

        /// <summary>死亡アニメーション時間（秒）</summary>
        public float DeathAnimDuration;

        /// <summary>攻撃範囲離脱倍率</summary>
        public float AttackRangeExitMultiplier;

        /// <summary>経験値</summary>
        public int ExperienceValue;

        /// <summary>敵タイプ（1:通常, 2:エリート, 3:ボス）</summary>
        public int EnemyType;

        /// <summary>アイテムドロップグループID</summary>
        public int ItemDropGroupId;

        /// <summary>経験値ドロップグループID</summary>
        public int ExpDropGroupId;
    }

    /// <summary>
    /// 敵ステータスデータ
    /// </summary>
    public struct EnemyData : IComponentData
    {
        /// <summary>敵マスターID</summary>
        public int EnemyId;

        /// <summary>敵タイプ（1:通常, 2:エリート, 3:ボス）</summary>
        public int EnemyType;

        /// <summary>現在HP</summary>
        public int CurrentHp;

        /// <summary>最大HP</summary>
        public int MaxHp;

        /// <summary>攻撃力</summary>
        public int AttackDamage;

        /// <summary>移動速度</summary>
        public float MoveSpeed;

        /// <summary>攻撃範囲</summary>
        public float AttackRange;

        /// <summary>攻撃クールダウン（秒）</summary>
        public float AttackCooldown;

        /// <summary>ヒットスタン時間（秒）</summary>
        public float HitStunDuration;

        /// <summary>回転速度</summary>
        public float RotationSpeed;

        /// <summary>死亡アニメーション時間（秒）</summary>
        public float DeathAnimDuration;

        /// <summary>攻撃範囲離脱倍率</summary>
        public float AttackRangeExitMultiplier;

        /// <summary>経験値</summary>
        public int ExperienceValue;

        /// <summary>アイテムドロップグループID</summary>
        public int ItemDropGroupId;

        /// <summary>経験値ドロップグループID</summary>
        public int ExpDropGroupId;
    }

    /// <summary>
    /// AI状態列挙
    /// </summary>
    public enum EcsEnemyAIStateType : byte
    {
        Chase = 0,
        Attack = 1,
        HitStun = 2,
        Dead = 3
    }

    /// <summary>
    /// AI状態コンポーネント
    /// </summary>
    public struct EnemyAIState : IComponentData
    {
        /// <summary>現在のAI状態</summary>
        public EcsEnemyAIStateType CurrentState;

        /// <summary>状態タイマー（Attack: クールダウン, HitStun: 残り時間）</summary>
        public float StateTimer;
    }

    /// <summary>
    /// 追尾対象座標
    /// </summary>
    public struct ChaseTarget : IComponentData
    {
        /// <summary>追尾対象のワールド座標</summary>
        public float3 Position;
    }

    /// <summary>
    /// 未処理ダメージイベント（武器から書き込まれる）
    /// </summary>
    public struct DamageEvent : IComponentData
    {
        /// <summary>未処理ダメージ量（0=ダメージなし）</summary>
        public int Damage;

        /// <summary>ノックバックベクトル</summary>
        public float3 Knockback;
    }

    /// <summary>
    /// 生存中タグ（Enableable: 有効/無効の切り替えでフィルタリング）
    /// </summary>
    public struct EnemyAliveTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// 死亡タグ（Enableable: DamageSystemで有効化→DeathCleanupで検出）
    /// </summary>
    public struct EnemyDeadTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// GameObject参照（ハイブリッド描画用、マネージド型）
    /// </summary>
    public class ManagedGameObjectReference : IComponentData
    {
        /// <summary>対応するGameObject</summary>
        public GameObject GameObject;

        /// <summary>EcsEnemyProxyコンポーネント参照</summary>
        public int GameObjectInstanceId;
    }
}
