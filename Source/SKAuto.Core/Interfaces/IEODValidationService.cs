using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IEODValidationService
    {
        Task<ValidationResult> ValidateDayAsync(DateTime date);
    }
}