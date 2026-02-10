using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SKAuto.Import.Validators
{
    public class ChassisValidator
    {
        private static readonly HashSet<string> ValidPrefixes = new()
        {
            "VF3", "VF7", "VF1", "VF2", // PSA prefixes
            "WDB", "WDD", "WDF", // Mercedes
            "WAU", "TRU", "WVG", "WVW", // Audi, Volkswagen
            "ZFF", // Ferrari
            // Add more as needed
        };

        public ValidationResult Validate(string chassisNumber)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(chassisNumber))
            {
                result.AddError("Chassis", "Chassis number cannot be empty");
                return result;
            }

            if (chassisNumber.Length < 10 || chassisNumber.Length > 17)
            {
                result.AddError("Chassis", $"Chassis number must be 10-17 characters, got {chassisNumber.Length}");
            }

            // Check for invalid characters
            if (chassisNumber.Any(c => !char.IsLetterOrDigit(c)))
            {
                result.AddError("Chassis", "Chassis number can only contain letters and digits");
            }

            // Check prefix if we have at least 3 characters
            if (chassisNumber.Length >= 3)
            {
                var prefix = chassisNumber.Substring(0, 3).ToUpper();
                if (!ValidPrefixes.Contains(prefix))
                {
                    result.AddWarning("Chassis", $"Unrecognized chassis prefix: {prefix}");
                }
            }

            // French VIN specific rules
            if (chassisNumber.StartsWith("VF") && chassisNumber.Length != 17)
            {
                result.AddWarning("Chassis", "French VIN should be 17 characters");
            }

            return result;
        }

        public bool IsValidFrenchVIN(string chassisNumber)
        {
            if (chassisNumber.Length != 17 || !chassisNumber.StartsWith("VF"))
                return false;

            // Basic French VIN validation
            return chassisNumber.All(c => char.IsLetterOrDigit(c)) &&
                   !chassisNumber.Contains('I') &&
                   !chassisNumber.Contains('O') &&
                   !chassisNumber.Contains('Q');
        }

        public string NormalizeChassis(string chassisNumber)
        {
            if (string.IsNullOrWhiteSpace(chassisNumber))
                return string.Empty;

            // Remove spaces, hyphens, convert to uppercase
            return chassisNumber
                .Replace(" ", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Replace(".", "", StringComparison.Ordinal)
                .ToUpperInvariant();
        }
    }
}