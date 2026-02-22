namespace Game.Server.Database.Migrations;

[FluentMigrator.Tags("User")]
[FluentMigrator.Migration(2026022300010001)]
public class _2026022300010001_AddUniqueDeviceFingerprintIndex : FluentMigrator.Migration
{
    private const string UserSchema = MigrationSchema.User;

    public override void Up()
    {
        // 既存の非UNIQUEインデックスを削除し、UNIQUE PARTIAL INDEXに置換
        Execute.Sql(@"DROP INDEX IF EXISTS ""User"".""IX_User_UserInfo_DeviceFingerprint""");

        Execute.Sql(
            @"CREATE UNIQUE INDEX ""IX_User_UserInfo_DeviceFingerprint""
              ON ""User"".""UserInfo"" (""DeviceFingerprint"")
              WHERE ""DeviceFingerprint"" IS NOT NULL");
    }

    public override void Down()
    {
        Execute.Sql(@"DROP INDEX IF EXISTS ""User"".""IX_User_UserInfo_DeviceFingerprint""");

        Execute.Sql(
            @"CREATE INDEX ""IX_User_UserInfo_DeviceFingerprint""
              ON ""User"".""UserInfo"" (""DeviceFingerprint"")
              WHERE ""DeviceFingerprint"" IS NOT NULL");
    }
}
