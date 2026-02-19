using Microsoft.EntityFrameworkCore;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Data.Database;
using System.Threading.Tasks;

namespace SKAuto.Data.Repository
{
    public class UserRepository : BaseRepository<User>, IRepository<User>
    {
        public UserRepository(DatabaseContext context) : base(context) { }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        }

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            var user = await GetByUsernameAsync(username);
            return user != null && user.PasswordHash == password; // Plain comparison for now
        }
    }
}