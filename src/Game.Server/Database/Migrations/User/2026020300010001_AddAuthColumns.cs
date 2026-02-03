namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("User")]
[FluentMigrator.Migration(2026020300010001)]
public class _2026020300010001_AddAuthColumns : FluentMigrator.Migration
{
    private const string UserSchema = MigrationSchema.User;

    public override void Up()
    {
        // Add new columns to UserInfo
        Alter.Table("UserInfo").InSchema(UserSchema)
            .AddColumn("Email").AsString(255).Nullable()
            .AddColumn("AuthType").AsString(20).NotNullable().WithDefaultValue("Password")
            .AddColumn("DeviceFingerprint").AsString(255).Nullable()
            .AddColumn("IsEmailVerified").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("EmailVerificationToken").AsString(255).Nullable()
            .AddColumn("EmailVerificationExpiry").AsDateTime2().Nullable()
            .AddColumn("PasswordResetToken").AsString(255).Nullable()
            .AddColumn("PasswordResetExpiry").AsDateTime2().Nullable()
            .AddColumn("FailedLoginAttempts").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("LockoutEndAt").AsDateTime2().Nullable();

        // Change PasswordHash to nullable (for Guest users)
        Alter.Column("PasswordHash").OnTable("UserInfo").InSchema(UserSchema)
            .AsString(255).Nullable();

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

        // Create UserExternalIdentity table
        Create.Table("UserExternalIdentity").InSchema(UserSchema)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("UserId").AsString(36).NotNullable()
            .WithColumn("Provider").AsString(50).NotNullable()
            .WithColumn("ProviderUserId").AsString(255).NotNullable()
            .WithColumn("ProviderData").AsCustom("text").Nullable()
            .WithColumn("LinkedAt").AsDateTime2().NotNullable();

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

        Execute.Sql(@"DROP INDEX IF EXISTS ""User"".""IX_User_UserInfo_Email""");
        Execute.Sql(@"DROP INDEX IF EXISTS ""User"".""IX_User_UserInfo_DeviceFingerprint""");

        // Revert PasswordHash to NOT NULL
        Alter.Column("PasswordHash").OnTable("UserInfo").InSchema(UserSchema)
            .AsString(255).NotNullable();

        Delete.Column("Email").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("AuthType").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("DeviceFingerprint").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("IsEmailVerified").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("EmailVerificationToken").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("EmailVerificationExpiry").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("PasswordResetToken").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("PasswordResetExpiry").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("FailedLoginAttempts").FromTable("UserInfo").InSchema(UserSchema);
        Delete.Column("LockoutEndAt").FromTable("UserInfo").InSchema(UserSchema);
    }
}
