using Game.Shared.Enums;
using Game.Shared.Interfaces;

namespace Game.Shared.Scriptable.Database.Tables
{
    public partial class HorrorItemMaster : IObjectInfo
    {
        public ObjectCategory ObjectCategory => ObjectCategory.Item;
    }
}
