using FluentMigrator;

namespace Game.Server.Data.Migrations;

[Migration(1)]
public class M0001_InitialCreate : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("DisplayName").AsString(50).NotNullable()
            .WithColumn("PasswordHash").AsString(255).NotNullable()
            .WithColumn("Level").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("LastLoginAt").AsDateTime2().NotNullable();

        Create.Index("IX_Users_DisplayName")
            .OnTable("Users")
            .OnColumn("DisplayName")
            .Ascending()
            .WithOptions().Unique();

        Create.Table("Scores")
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("UserId").AsString(36).NotNullable()
            .WithColumn("GameMode").AsString(50).NotNullable()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("Score").AsInt32().NotNullable()
            .WithColumn("ClearTime").AsFloat().NotNullable()
            .WithColumn("WaveReached").AsInt32().NotNullable()
            .WithColumn("EnemiesDefeated").AsInt32().NotNullable()
            .WithColumn("RecordedAt").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_Scores_Users_UserId")
            .FromTable("Scores").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_Scores_GameMode_StageId_Score")
            .OnTable("Scores")
            .OnColumn("GameMode").Ascending()
            .OnColumn("StageId").Ascending()
            .OnColumn("Score").Descending();

        Create.Index("IX_Scores_UserId_GameMode_StageId")
            .OnTable("Scores")
            .OnColumn("UserId").Ascending()
            .OnColumn("GameMode").Ascending()
            .OnColumn("StageId").Ascending();
    }

    public override void Down()
    {
        Delete.Table("Scores");
        Delete.Table("Users");
    }
}
