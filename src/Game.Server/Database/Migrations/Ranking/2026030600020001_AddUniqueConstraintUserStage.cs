namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("Ranking")]
[FluentMigrator.Migration(2026030600020001)]
public class _2026030600020001_AddUniqueConstraintUserStage : FluentMigrator.Migration
{
    private const string RankingSchema = MigrationSchema.Ranking;

    public override void Up()
    {
        // 既存の非UNIQUEインデックスをUNIQUEに置換
        Execute.Sql(@"DROP INDEX IF EXISTS ""Ranking"".""IX_Ranking_SurvivorScore_UserId_StageId""");

        Execute.Sql(
            @"CREATE UNIQUE INDEX ""IX_Ranking_SurvivorScore_UserId_StageId""
              ON ""Ranking"".""SurvivorScore"" (""UserId"", ""StageId"")");
    }

    public override void Down()
    {
        Execute.Sql(@"DROP INDEX IF EXISTS ""Ranking"".""IX_Ranking_SurvivorScore_UserId_StageId""");

        Create.Index("IX_Ranking_SurvivorScore_UserId_StageId")
            .OnTable("SurvivorScore").InSchema(RankingSchema)
            .OnColumn("UserId").Ascending()
            .OnColumn("StageId").Ascending();
    }
}
