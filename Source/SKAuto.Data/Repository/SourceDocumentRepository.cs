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
    public class SourceDocumentRepository : BaseRepository<SourceDocument>, IRepository<SourceDocument>
    {
        public SourceDocumentRepository(DatabaseContext context) : base(context) { }
    }
}