using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class ProtectedRateRepository : BaseRepository<ProtectedRate>, IRepository<ProtectedRate>
    {
        public ProtectedRateRepository(DatabaseContext context) : base(context) { }
    }
}