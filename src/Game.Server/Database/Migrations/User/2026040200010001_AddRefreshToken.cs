namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("User")]
[FluentMigrator.Migration(2026040200010001)]
public class _2026040200010001_AddRefreshToken : FluentMigrator.Migration
{
    private const string UserSchema = MigrationSchema.User;

    public override void Up()
    {
        Alter.Table("UserInfo").InSchema(UserSchema)
            .AddColumn("RefreshTokenHash").AsString(128).Nullable()
            .AddColumn("RefreshTokenExpiry").AsCustom("timestamptz").Nullable();

        Execute.Sql(
            @"CREATE INDEX ""IX_User_UserInfo_RefreshTokenHash""
              ON ""User"".""UserInfo"" (""RefreshTokenHash"")
              WHERE ""RefreshTokenHash"" IS NOT NULL");
    }

    public override void Down()
    {
        Execute.Sql(@"DROP INDEX IF EXISTS ""User"".""IX_User_UserInfo_RefreshTokenHash""");

        Delete.Column("RefreshTokenHash").Column("RefreshTokenExpiry")
            .FromTable("UserInfo").InSchema(UserSchema);
    }
}
