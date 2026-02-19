using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Entities
{
    public class ProtectedRate : BaseEntity
    {
        public int AccessoryId { get; set; }
        public DateTime ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }
        public decimal HourlyRate { get; set; }
        public string Currency { get; set; } = "EUR";
        public byte[]? EncryptedRate { get; set; } // Optional encryption

        // Navigation property
        public virtual Accessory Accessory { get; set; } = null!;
    }
}
