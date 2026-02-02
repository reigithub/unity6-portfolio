using System.Data;

namespace Game.Server.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
