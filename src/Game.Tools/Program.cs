using ConsoleAppFramework;
using Game.Tools.Commands;

namespace Game.Tools;

public partial class Program
{
    public static void Main(string[] args)
    {
        var app = ConsoleApp.Create();
        app.Add<MasterDataCommands>("masterdata");
        app.Add<MigrateCommands>("migrate");
        app.Add<SeedDataCommands>("seeddata");
        app.Run(args);
    }
}
