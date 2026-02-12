using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SKAuto.Core.Enums;

namespace SKAuto.Core.Entities
{
    public class Client : BaseEntity
    {
        public string ClientCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ClientType Type { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string Notes { get; set; } = string.Empty; 
        public bool IsActive { get; set; } = true;

        public string? ContactNumber { get; set; }
        public string? Email { get; set; }

        // Navigation properties
        public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();

        // Business methods
        public bool IsPSA => Type == ClientType.PSA;
    }
}