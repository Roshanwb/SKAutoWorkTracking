using Microsoft.EntityFrameworkCore;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;

namespace SKAuto.Data.Repository
{
    public class VehicleRepository : BaseRepository<Vehicle>
    {
        public VehicleRepository(DatabaseContext context) : base(context) { }

        public async Task<Vehicle?> GetByChassisAsync(string chassisNumber)
        {
            return await _dbSet
                .FirstOrDefaultAsync(v => v.ChassisNumber == chassisNumber);
        }

        public async Task<IEnumerable<Vehicle>> SearchAsync(string term)
        {
            return await _dbSet
                .Where(v => v.ChassisNumber.Contains(term) ||
                            (v.Model != null && v.Model.Contains(term)))
                .Take(20)
                .ToListAsync();
        }

        public async Task<bool> ExistsAsync(string chassisNumber)
        {
            return await _dbSet.AnyAsync(v => v.ChassisNumber == chassisNumber);
        }
        public async Task<List<Vehicle>> GetByChassisNumbersAsync(IEnumerable<string> chassisNumbers)
        {
            return await _dbSet.Where(v => chassisNumbers.Contains(v.ChassisNumber)).ToListAsync();
        }
        public async Task<Vehicle?> GetByChassisWithClientAsync(string chassis)
        {
            return await _dbSet
                .Include(v => v.Client)
                .FirstOrDefaultAsync(v => v.ChassisNumber == chassis);
        }
    }
}