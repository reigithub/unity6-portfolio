namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("Ranking")]
[FluentMigrator.Migration(2026030600010001)]
public class _2026030600010001_FixSurvivorScoreIdentitySequence : FluentMigrator.Migration
{
    public override void Up()
    {
        // IDENTITY シーケンスが既存データの MAX(Id) より遅れている場合にリセット
        // これにより INSERT 時の duplicate key violation (23505) を防止する
        Execute.Sql(
            @"SELECT setval(
                pg_get_serial_sequence('""Ranking"".""SurvivorScore""', 'Id'),
                COALESCE((SELECT MAX(""Id"") FROM ""Ranking"".""SurvivorScore""), 0)
              )");
    }

    public override void Down()
    {
        // シーケンスリセットは元に戻す必要なし
    }
}
