using Microsoft.EntityFrameworkCore;
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
    public class AccessoryRepository : BaseRepository<Accessory>, IRepository<Accessory>
    {
        public AccessoryRepository(DatabaseContext context) : base(context) { }

        // Add specialized methods here if needed
    }
}