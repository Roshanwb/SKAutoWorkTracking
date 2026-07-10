using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class WorkTaskRepository : BaseRepository<WorkTask>, IRepository<WorkTask>
    {
        public WorkTaskRepository(DatabaseContext context) : base(context) { }
    }
}