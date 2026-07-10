using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class TravelRepository : BaseRepository<Travel>, IRepository<Travel>
    {
        public TravelRepository(DatabaseContext context) : base(context)
        {
        }
    }
}