using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class AccessoryRepository : BaseRepository<Accessory>, IRepository<Accessory>
    {
        public AccessoryRepository(DatabaseContext context) : base(context) { }

        // Add specialized methods here if needed
    }
}