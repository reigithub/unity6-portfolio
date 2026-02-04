namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("User")]
[FluentMigrator.Migration(2026020100010001)]
public class _2026020100010001_CreateUserSchema : FluentMigrator.Migration
{
    private const string UserSchema = MigrationSchema.User;

    public override void Up()
    {
        Create.Table("UserInfo").InSchema(UserSchema)
            .WithColumn("Id").AsGuid().PrimaryKey()
            .WithColumn("UserId").AsString(36).NotNullable().Unique()
            .WithColumn("UserName").AsString(50).NotNullable()
            .WithColumn("PasswordHash").AsString(255).Nullable()
            .WithColumn("Level").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("CreatedAt").AsCustom("timestamptz").NotNullable()
            .WithColumn("LastLoginAt").AsCustom("timestamptz").NotNullable()
            .WithColumn("Email").AsString(255).Nullable()
            .WithColumn("AuthType").AsString(20).NotNullable().WithDefaultValue("Password")
            .WithColumn("DeviceFingerprint").AsString(255).Nullable()
            .WithColumn("IsEmailVerified").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("EmailVerificationToken").AsString(255).Nullable()
            .WithColumn("EmailVerificationExpiry").AsCustom("timestamptz").Nullable()
            .WithColumn("PasswordResetToken").AsString(255).Nullable()
            .WithColumn("PasswordResetExpiry").AsCustom("timestamptz").Nullable()
            .WithColumn("FailedLoginAttempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LockoutEndAt").AsCustom("timestamptz").Nullable();

        Create.Index("IX_User_UserInfo_UserName")
            .OnTable("UserInfo").InSchema(UserSchema)
            .OnColumn("UserName")
            .Ascending()
            .WithOptions().Unique();

        // Partial unique index on Email (WHERE Email IS NOT NULL)
        Execute.Sql(
            @"CREATE UNIQUE INDEX ""IX_User_UserInfo_Email""
              ON ""User"".""UserInfo"" (""Email"")
              WHERE ""Email"" IS NOT NULL");

        // Partial index on DeviceFingerprint
        Execute.Sql(
            @"CREATE INDEX ""IX_User_UserInfo_DeviceFingerprint""
              ON ""User"".""UserInfo"" (""DeviceFingerprint"")
              WHERE ""DeviceFingerprint"" IS NOT NULL");

        Create.Table("UserScore").InSchema(UserSchema)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("GameMode").AsString(50).NotNullable()
            .WithColumn("StageId").AsInt32().NotNullable()
            .WithColumn("Score").AsInt32().NotNullable()
            .WithColumn("ClearTime").AsFloat().NotNullable()
            .WithColumn("WaveReached").AsInt32().NotNullable()
            .WithColumn("EnemiesDefeated").AsInt32().NotNullable()
            .WithColumn("RecordedAt").AsCustom("timestamptz").NotNullable();

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

        Create.Table("UserExternalIdentity").InSchema(UserSchema)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("UserId").AsGuid().NotNullable()
            .WithColumn("Provider").AsString(50).NotNullable()
            .WithColumn("ProviderUserId").AsString(255).NotNullable()
            .WithColumn("ProviderData").AsCustom("text").Nullable()
            .WithColumn("LinkedAt").AsCustom("timestamptz").NotNullable();

        Create.ForeignKey("FK_User_UserExternalIdentity_UserInfo_UserId")
            .FromTable("UserExternalIdentity").InSchema(UserSchema).ForeignColumn("UserId")
            .ToTable("UserInfo").InSchema(UserSchema).PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("UQ_User_UserExternalIdentity_Provider_ProviderUserId")
            .OnTable("UserExternalIdentity").InSchema(UserSchema)
            .OnColumn("Provider").Ascending()
            .OnColumn("ProviderUserId").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("UserExternalIdentity").InSchema(UserSchema);
        Delete.Table("UserScore").InSchema(UserSchema);
        Delete.Table("UserInfo").InSchema(UserSchema);
    }
}
