using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Entities
{
    public class SourceDocument : BaseEntity
    {
        public int WorkOrderId { get; set; }
        public string DocumentType { get; set; } = string.Empty; // "PSA_Plan", "Client_Order", "Invoice"
        public string FilePath { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty; // SHA256
        public string OriginalFilename { get; set; } = string.Empty;

        // Navigation property
        public virtual WorkOrder WorkOrder { get; set; } = null!;
    }
}