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
    public class VehicleRepository : BaseRepository<Vehicle>, IRepository<Vehicle>
    {
        public VehicleRepository(DatabaseContext context) : base(context) { }

        public async Task<Vehicle?> GetByChassisAsync(string chassisNumber)
        {
            return await _dbSet.FirstOrDefaultAsync(v => v.ChassisNumber == chassisNumber);
        }

        public async Task<IEnumerable<Vehicle>> SearchAsync(string searchTerm)
        {
            return await _dbSet
                .Where(v => v.ChassisNumber.Contains(searchTerm) ||
                           v.Model.Contains(searchTerm) ||
                           v.Registration != null && v.Registration.Contains(searchTerm))
                .Take(50)
                .ToListAsync();
        }

        public async Task<bool> ChassisExistsAsync(string chassisNumber)
        {
            return await _dbSet.AnyAsync(v => v.ChassisNumber == chassisNumber);
        }
    }
}
