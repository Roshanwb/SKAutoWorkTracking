using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Data.Repository
{
    public class ProtectedRateRepository : BaseRepository<ProtectedRate>, IRepository<ProtectedRate>
    {
        public ProtectedRateRepository(DatabaseContext context) : base(context) { }
    }
}