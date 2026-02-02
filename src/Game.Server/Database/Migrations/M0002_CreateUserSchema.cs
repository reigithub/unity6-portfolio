using FluentMigrator;

namespace Game.Server.Database.Migrations;

[Migration(2)]
public class M0002_CreateUserSchema : Migration
{
    private const string UserSchema = "User";

    public override void Up()
    {
        if (!Schema.Schema(UserSchema).Exists())
            Create.Schema(UserSchema);

        Create.Table("UserInfo").InSchema(UserSchema)
            .WithColumn("Id").AsString(36).PrimaryKey()
            .WithColumn("DisplayName").AsString(50).NotNullable()
            .WithColumn("PasswordHash").AsString(255).NotNullable()
            .WithColumn("Level").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("CreatedAt").AsDateTime2().NotNullable()
            .WithColumn("LastLoginAt").AsDateTime2().NotNullable();

        Create.Index("IX_User_UserInfo_DisplayName")
            .OnTable("UserInfo").InSchema(UserSchema)
            .OnColumn("DisplayName")
            .Ascending()
            .WithOptions().Unique();

        Create.Table("UserScore").InSchema(UserSchema)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("UserId").AsString(36).NotNullable()
            .WithColumn("GameMode").AsString(50).NotNullable()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("Score").AsInt32().NotNullable()
            .WithColumn("ClearTime").AsFloat().NotNullable()
            .WithColumn("WaveReached").AsInt32().NotNullable()
            .WithColumn("EnemiesDefeated").AsInt32().NotNullable()
            .WithColumn("RecordedAt").AsDateTime2().NotNullable();

        Create.ForeignKey("FK_User_UserScore_UserInfo_UserId")
            .FromTable("UserScore").InSchema(UserSchema).ForeignColumn("UserId")
            .ToTable("UserInfo").InSchema(UserSchema).PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_User_UserScore_GameMode_StageId_Score")
            .OnTable("UserScore").InSchema(UserSchema)
            .OnColumn("GameMode").Ascending()
            .OnColumn("StageId").Ascending()
            .OnColumn("Score").Descending();

        Create.Index("IX_User_UserScore_UserId_GameMode_StageId")
            .OnTable("UserScore").InSchema(UserSchema)
            .OnColumn("UserId").Ascending()
            .OnColumn("GameMode").Ascending()
            .OnColumn("StageId").Ascending();
    }

    public override void Down()
    {
        if (!Schema.Schema(UserSchema).Exists())
            return;

        Delete.Table("UserScore").InSchema(UserSchema);
        Delete.Table("UserInfo").InSchema(UserSchema);
        Delete.Schema(UserSchema);
    }
}
