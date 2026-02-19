using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class ClientRepository : BaseRepository<Client>, IRepository<Client>
    {
        public ClientRepository(DatabaseContext context) : base(context) { }

        public async Task<Client?> GetByNameAsync(string name)
        {
            return await _dbSet.FirstOrDefaultAsync(c => c.Name == name);
        }

        public async Task<IEnumerable<Client>> GetPSAClientsAsync()
        {
            return await _dbSet.Where(c => c.Type == Core.Enums.ClientType.PSA).ToListAsync();
        }

        public async Task<IEnumerable<Client>> GetDirectClientsAsync()
        {
            return await _dbSet.Where(c => c.Type == Core.Enums.ClientType.Direct).ToListAsync();
        }
    }
}
