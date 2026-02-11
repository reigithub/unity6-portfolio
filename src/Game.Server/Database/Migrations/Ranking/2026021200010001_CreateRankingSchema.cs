namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("Ranking")]
[FluentMigrator.Migration(2026021200010001)]
public class _2026021200010001_CreateRankingSchema : FluentMigrator.Migration
{
    private const string RankingSchema = MigrationSchema.Ranking;
    private const string UserSchema = MigrationSchema.User;

    public override void Up()
    {
        Create.Table("SurvivorScore").InSchema(RankingSchema)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("Score").AsInt32().NotNullable()
            .WithColumn("ClearTime").AsFloat().NotNullable()
            .WithColumn("WaveReached").AsInt32().NotNullable()
            .WithColumn("EnemiesDefeated").AsInt32().NotNullable()
            .WithColumn("RecordedAt").AsCustom("timestamptz").NotNullable()
            .WithColumn("CreatedAt").AsCustom("timestamptz").NotNullable().WithDefault(FluentMigrator.SystemMethods.CurrentDateTime)
            .WithColumn("UpdatedAt").AsCustom("timestamptz").NotNullable().WithDefault(FluentMigrator.SystemMethods.CurrentDateTime);

        Create.ForeignKey("FK_Ranking_SurvivorScore_UserInfo_UserId")
            .FromTable("SurvivorScore").InSchema(RankingSchema).ForeignColumn("UserId")
            .ToTable("UserInfo").InSchema(UserSchema).PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_Ranking_SurvivorScore_StageId_Score")
            .OnTable("SurvivorScore").InSchema(RankingSchema)
            .OnColumn("StageId").Ascending()
            .OnColumn("Score").Descending();

        Create.Index("IX_Ranking_SurvivorScore_UserId_StageId")
            .OnTable("SurvivorScore").InSchema(RankingSchema)
            .OnColumn("UserId").Ascending()
            .OnColumn("StageId").Ascending();

        // Trigger function for auto-updating UpdatedAt
        Execute.Sql(
            @"CREATE OR REPLACE FUNCTION ""Ranking"".set_updated_at()
              RETURNS TRIGGER AS $$
              BEGIN
                  NEW.""UpdatedAt"" = now();
                  RETURN NEW;
              END;
              $$ LANGUAGE plpgsql");

        Execute.Sql(
            @"CREATE TRIGGER trg_survivorscore_updated_at BEFORE UPDATE ON ""Ranking"".""SurvivorScore""
              FOR EACH ROW EXECUTE FUNCTION ""Ranking"".set_updated_at()");
    }

    public override void Down()
    {
        Execute.Sql(@"DROP TRIGGER IF EXISTS trg_survivorscore_updated_at ON ""Ranking"".""SurvivorScore""");
        Execute.Sql(@"DROP FUNCTION IF EXISTS ""Ranking"".set_updated_at()");

        Delete.Table("SurvivorScore").InSchema(RankingSchema);
    }
}
