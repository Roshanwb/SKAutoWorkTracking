using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace SKAuto.Core.Entities
{
    public class Vehicle : BaseEntity
    {
        public string ChassisNumber { get; set; } = string.Empty;
        public int ClientId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string? Make { get; set; }
        public int? Year { get; set; }
        public string? Registration { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
        public virtual Client Client { get; set; } = null!;


        // Business methods
        public string GetDisplayName() => $"{Make} {Model} ({ChassisNumber})";
    }
}