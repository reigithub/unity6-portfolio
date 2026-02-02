using FluentMigrator;

namespace Game.Server.Database.Migrations;

[Migration(1)]
public class M0001_CreateMasterSchema : Migration
{
    private const string MasterSchema = "Master";

    public override void Up()
    {
        Create.Schema(MasterSchema);

        // ── Audio ──────────────────────────────────────────────
        Create.Table("AudioMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("Desc").AsString(500).NotNullable()
            .WithColumn("AudioCategory").AsInt32().NotNullable();

        Create.Index("IX_Master_AudioMaster_AudioCategory")
            .OnTable("AudioMaster").InSchema(MasterSchema)
            .OnColumn("AudioCategory").Ascending();

        Create.Table("AudioPlayTagsMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("AudioId").AsInt32().NotNullable()
            .WithColumn("AudioPlayTag").AsInt32().NotNullable();

        Create.Index("IX_Master_AudioPlayTagsMaster_AudioPlayTag")
            .OnTable("AudioPlayTagsMaster").InSchema(MasterSchema)
            .OnColumn("AudioPlayTag").Ascending();

        // ── Survivor ───────────────────────────────────────────
        Create.Table("SurvivorPlayerMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("StartingWeaponId").AsInt32().NotNullable();

        Create.Table("SurvivorPlayerLevelMaster").InSchema(MasterSchema)
            .WithColumn("PlayerId").AsInt32().NotNullable()
            .WithColumn("Level").AsInt32().NotNullable()
            .WithColumn("RequiredExp").AsInt32().NotNullable()
            .WithColumn("MaxHp").AsInt32().NotNullable()
            .WithColumn("MaxStamina").AsInt32().NotNullable()
            .WithColumn("StaminaDepleteRate").AsInt32().NotNullable()
            .WithColumn("StaminaRegenRate").AsInt32().NotNullable()
            .WithColumn("MoveSpeed").AsInt32().NotNullable()
            .WithColumn("RunSpeed").AsInt32().NotNullable()
            .WithColumn("PickupRange").AsInt32().NotNullable()
            .WithColumn("CritRate").AsInt32().NotNullable()
            .WithColumn("CritDamage").AsInt32().NotNullable()
            .WithColumn("InvincibilityDuration").AsInt32().NotNullable()
            .WithColumn("ItemAttractDistance").AsInt32().NotNullable()
            .WithColumn("ItemAttractSpeed").AsInt32().NotNullable()
            .WithColumn("ItemCollectDistance").AsInt32().NotNullable()
            .WithColumn("DamageBonus").AsInt32().NotNullable()
            .WithColumn("WeaponChoiceCount").AsInt32().NotNullable();

        Create.PrimaryKey("PK_Master_SurvivorPlayerLevelMaster")
            .OnTable("SurvivorPlayerLevelMaster").WithSchema(MasterSchema)
            .Columns("PlayerId", "Level");

        Create.Table("SurvivorStageMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("ThumbnailAssetName").AsString(200).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable()
            .WithColumn("TimeLimit").AsInt32().NotNullable()
            .WithColumn("PlayerId").AsInt32().NotNullable()
            .WithColumn("BgmAssetName").AsString(200).NotNullable()
            .WithColumn("UnlockStageId").AsInt32().Nullable()
            .WithColumn("Difficulty").AsInt32().NotNullable();

        Create.Table("SurvivorEnemyMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("EnemyType").AsInt32().NotNullable()
            .WithColumn("BaseHp").AsInt32().NotNullable()
            .WithColumn("BaseDamage").AsInt32().NotNullable()
            .WithColumn("MoveSpeed").AsInt32().NotNullable()
            .WithColumn("AttackRange").AsInt32().NotNullable()
            .WithColumn("AttackCooldown").AsInt32().NotNullable()
            .WithColumn("HitStunDuration").AsInt32().NotNullable()
            .WithColumn("RotationSpeed").AsInt32().NotNullable()
            .WithColumn("DeathAnimDuration").AsInt32().NotNullable()
            .WithColumn("ExperienceValue").AsInt32().NotNullable()
            .WithColumn("DropItemId").AsInt32().Nullable()
            .WithColumn("DropRate").AsInt32().NotNullable()
            .WithColumn("MaxConcurrent").AsInt32().NotNullable()
            .WithColumn("SpawnRadius").AsInt32().NotNullable()
            .WithColumn("AttackRangeExitMultiplier").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorEnemyMaster_EnemyType")
            .OnTable("SurvivorEnemyMaster").InSchema(MasterSchema)
            .OnColumn("EnemyType").Ascending();

        Create.Table("SurvivorWeaponMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("WeaponType").AsInt32().NotNullable()
            .WithColumn("Rarity").AsInt32().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("IconAssetName").AsString(200).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable()
            .WithColumn("HitEffectAssetName").AsString(200).NotNullable()
            .WithColumn("HitEffectScale").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorWeaponMaster_WeaponType")
            .OnTable("SurvivorWeaponMaster").InSchema(MasterSchema)
            .OnColumn("WeaponType").Ascending();

        Create.Table("SurvivorWeaponLevelMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("WeaponId").AsInt32().NotNullable()
            .WithColumn("Level").AsInt32().NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("Description").AsString(500).NotNullable()
            .WithColumn("Damage").AsInt32().NotNullable()
            .WithColumn("Range").AsInt32().NotNullable()
            .WithColumn("Speed").AsInt32().NotNullable()
            .WithColumn("Duration").AsInt32().NotNullable()
            .WithColumn("Cooldown").AsInt32().NotNullable()
            .WithColumn("ProcRate").AsInt32().NotNullable()
            .WithColumn("ProcInterval").AsInt32().NotNullable()
            .WithColumn("EmitCount").AsInt32().NotNullable()
            .WithColumn("EmitDelay").AsInt32().NotNullable()
            .WithColumn("EmitLimit").AsInt32().NotNullable()
            .WithColumn("HitCount").AsInt32().NotNullable()
            .WithColumn("HitBoxRate").AsInt32().NotNullable()
            .WithColumn("CritHitRate").AsInt32().NotNullable()
            .WithColumn("CritHitMultiplier").AsInt32().NotNullable()
            .WithColumn("Knockback").AsInt32().NotNullable()
            .WithColumn("Vacuum").AsInt32().NotNullable()
            .WithColumn("Spin").AsInt32().NotNullable()
            .WithColumn("Penetration").AsInt32().NotNullable()
            .WithColumn("Bounce").AsInt32().NotNullable()
            .WithColumn("Chain").AsInt32().NotNullable()
            .WithColumn("Homing").AsInt32().NotNullable()
            .WithColumn("Spread").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorWeaponLevelMaster_WeaponId")
            .OnTable("SurvivorWeaponLevelMaster").InSchema(MasterSchema)
            .OnColumn("WeaponId").Ascending();

        Create.Table("SurvivorItemMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("ItemType").AsInt32().NotNullable()
            .WithColumn("EffectValue").AsInt32().NotNullable()
            .WithColumn("EffectRange").AsInt32().NotNullable()
            .WithColumn("EffectDuration").AsInt32().NotNullable()
            .WithColumn("Rarity").AsInt32().NotNullable()
            .WithColumn("Scale").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorItemMaster_ItemType")
            .OnTable("SurvivorItemMaster").InSchema(MasterSchema)
            .OnColumn("ItemType").Ascending();

        Create.Table("SurvivorItemDropMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("GroupId").AsInt32().NotNullable()
            .WithColumn("ItemId").AsInt32().NotNullable()
            .WithColumn("DropRate").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorItemDropMaster_GroupId")
            .OnTable("SurvivorItemDropMaster").InSchema(MasterSchema)
            .OnColumn("GroupId").Ascending();

        Create.Table("SurvivorStageWaveMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("WaveNumber").AsInt32().NotNullable()
            .WithColumn("StartTime").AsInt32().NotNullable()
            .WithColumn("Duration").AsInt32().NotNullable()
            .WithColumn("EnemySpeedMultiplier").AsInt32().NotNullable()
            .WithColumn("EnemyHealthMultiplier").AsInt32().NotNullable()
            .WithColumn("EnemyDamageMultiplier").AsInt32().NotNullable()
            .WithColumn("ExperienceMultiplier").AsInt32().NotNullable()
            .WithColumn("TargetKillCount").AsInt32().NotNullable()
            .WithColumn("RequiredBossKills").AsInt32().NotNullable()
            .WithColumn("ScoreMultiplier").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorStageWaveMaster_StageId")
            .OnTable("SurvivorStageWaveMaster").InSchema(MasterSchema)
            .OnColumn("StageId").Ascending();

        Create.Table("SurvivorStageWaveEnemyMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("WaveId").AsInt32().NotNullable()
            .WithColumn("EnemyId").AsInt32().NotNullable()
            .WithColumn("SpawnCount").AsInt32().NotNullable()
            .WithColumn("SpawnInterval").AsInt32().NotNullable()
            .WithColumn("SpawnDelay").AsInt32().NotNullable()
            .WithColumn("MinSpawnDistance").AsInt32().NotNullable()
            .WithColumn("MaxSpawnDistance").AsInt32().NotNullable()
            .WithColumn("ItemDropGroupId").AsInt32().NotNullable()
            .WithColumn("ExpDropGroupId").AsInt32().NotNullable();

        Create.Index("IX_Master_SurvivorStageWaveEnemyMaster_WaveId")
            .OnTable("SurvivorStageWaveEnemyMaster").InSchema(MasterSchema)
            .OnColumn("WaveId").Ascending();

        // ── ScoreTimeAttack ────────────────────────────────────
        Create.Table("ScoreTimeAttackPlayerMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("MaxHp").AsInt32().NotNullable()
            .WithColumn("MaxStamina").AsInt32().NotNullable()
            .WithColumn("StaminaDepleteRate").AsInt32().NotNullable()
            .WithColumn("StaminaRegenRate").AsInt32().NotNullable()
            .WithColumn("WalkSpeed").AsInt32().NotNullable()
            .WithColumn("JogSpeed").AsInt32().NotNullable()
            .WithColumn("RunSpeed").AsInt32().NotNullable()
            .WithColumn("Jump").AsInt32().NotNullable();

        Create.Table("ScoreTimeAttackStageMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("GroupId").AsInt32().NotNullable()
            .WithColumn("Order").AsInt32().NotNullable()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("TotalTime").AsInt32().NotNullable()
            .WithColumn("MaxPoint").AsInt32().NotNullable()
            .WithColumn("PlayerId").AsInt32().Nullable()
            .WithColumn("NextStageId").AsInt32().Nullable();

        Create.Index("IX_Master_ScoreTimeAttackStageMaster_GroupId")
            .OnTable("ScoreTimeAttackStageMaster").InSchema(MasterSchema)
            .OnColumn("GroupId").Ascending();

        Create.Table("ScoreTimeAttackEnemyMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("WalkSpeed").AsInt32().NotNullable()
            .WithColumn("RunSpeed").AsInt32().NotNullable()
            .WithColumn("VisualDistance").AsInt32().NotNullable()
            .WithColumn("AuditoryDistance").AsInt32().NotNullable()
            .WithColumn("HpAttack").AsInt32().NotNullable();

        Create.Table("ScoreTimeAttackEnemySpawnMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("GroupId").AsInt32().NotNullable()
            .WithColumn("EnemyId").AsInt32().NotNullable()
            .WithColumn("X").AsInt32().NotNullable()
            .WithColumn("Y").AsInt32().NotNullable()
            .WithColumn("Z").AsInt32().NotNullable()
            .WithColumn("MinSpawnCount").AsInt32().NotNullable()
            .WithColumn("MaxSpawnCount").AsInt32().NotNullable();

        Create.Index("IX_Master_ScoreTimeAttackEnemySpawnMaster_StageId")
            .OnTable("ScoreTimeAttackEnemySpawnMaster").InSchema(MasterSchema)
            .OnColumn("StageId").Ascending();

        Create.Table("ScoreTimeAttackStageItemMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("Name").AsString(200).NotNullable()
            .WithColumn("AssetName").AsString(200).NotNullable()
            .WithColumn("Point").AsInt32().NotNullable();

        Create.Index("IX_Master_ScoreTimeAttackStageItemMaster_AssetName")
            .OnTable("ScoreTimeAttackStageItemMaster").InSchema(MasterSchema)
            .OnColumn("AssetName").Ascending();

        Create.Table("ScoreTimeAttackStageItemSpawnMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("GroupId").AsInt32().NotNullable()
            .WithColumn("StageItemId").AsInt32().NotNullable()
            .WithColumn("X").AsInt32().NotNullable()
            .WithColumn("Y").AsInt32().NotNullable()
            .WithColumn("Z").AsInt32().NotNullable()
            .WithColumn("MinSpawnCount").AsInt32().NotNullable()
            .WithColumn("MaxSpawnCount").AsInt32().NotNullable();

        Create.Index("IX_Master_ScoreTimeAttackStageItemSpawnMaster_StageId")
            .OnTable("ScoreTimeAttackStageItemSpawnMaster").InSchema(MasterSchema)
            .OnColumn("StageId").Ascending();

        Create.Table("ScoreTimeAttackStageTotalResultMaster").InSchema(MasterSchema)
            .WithColumn("Id").AsInt32().PrimaryKey()
            .WithColumn("TotalScore").AsInt32().NotNullable()
            .WithColumn("TotalRank").AsString(50).NotNullable()
            .WithColumn("AnimatorStateName").AsString(200).NotNullable()
            .WithColumn("BgmAudioId").AsInt32().NotNullable()
            .WithColumn("VoiceAudioId").AsInt32().NotNullable()
            .WithColumn("SoundEffectAudioId").AsInt32().NotNullable();
    }

    public override void Down()
    {
        Delete.Table("ScoreTimeAttackStageTotalResultMaster").InSchema(MasterSchema);
        Delete.Table("ScoreTimeAttackStageItemSpawnMaster").InSchema(MasterSchema);
        Delete.Table("ScoreTimeAttackStageItemMaster").InSchema(MasterSchema);
        Delete.Table("ScoreTimeAttackEnemySpawnMaster").InSchema(MasterSchema);
        Delete.Table("ScoreTimeAttackEnemyMaster").InSchema(MasterSchema);
        Delete.Table("ScoreTimeAttackStageMaster").InSchema(MasterSchema);
        Delete.Table("ScoreTimeAttackPlayerMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorStageWaveEnemyMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorStageWaveMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorItemDropMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorItemMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorWeaponLevelMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorWeaponMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorEnemyMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorStageMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorPlayerLevelMaster").InSchema(MasterSchema);
        Delete.Table("SurvivorPlayerMaster").InSchema(MasterSchema);
        Delete.Table("AudioPlayTagsMaster").InSchema(MasterSchema);
        Delete.Table("AudioMaster").InSchema(MasterSchema);
        Delete.Schema(MasterSchema);
    }
}
