using System.Data;

namespace Game.Server.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
