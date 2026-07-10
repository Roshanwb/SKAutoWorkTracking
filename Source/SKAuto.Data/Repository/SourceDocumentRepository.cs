using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class SourceDocumentRepository : BaseRepository<SourceDocument>, IRepository<SourceDocument>
    {
        public SourceDocumentRepository(DatabaseContext context) : base(context) { }
    }
}