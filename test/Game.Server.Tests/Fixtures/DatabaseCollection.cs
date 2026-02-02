namespace Game.Server.Tests.Fixtures;

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgresContainerFixture>
{
}
