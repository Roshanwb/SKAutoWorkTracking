using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; } // store plain for now; later hash
        public string Role { get; set; } // "Admin" or "User"
        public bool IsActive { get; set; } = true;
    }
}
