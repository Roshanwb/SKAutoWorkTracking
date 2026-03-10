using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKAuto.Core.Enums;
using System.Collections.Generic;

namespace SKAuto.Core.Entities
{
    public class Client : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public int? ParentClientId { get; set; }
        public string? ClientCode { get; set; }
        public ClientType Type { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual Client? ParentClient { get; set; }
        public virtual ICollection<Client> ChildClients { get; set; } = new List<Client>();
        public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();   // Added – each client has many vehicles
        // WorkOrders collection removed – now accessed via Vehicle
    }
}