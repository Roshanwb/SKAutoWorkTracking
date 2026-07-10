using SKAuto.Core.Interfaces;

namespace SKAuto.Import.Validators
{
    public class ChassisValidator : IChassisValidator
    {
        private static readonly HashSet<string> ValidFrenchPrefixes = new()
        {
            "VF1", "VF2", "VF3", "VF4", "VF5", "VF6", "VF7", "VF8", // PSA (Peugeot, Citroën, DS, Opel)
            "VF9", // Other
            "VNE", // Renault
            "VNK", // Renault Trucks
            "VR1", // Renault (alternate)
            "VSA", // Iveco (French production)
            "YV1", "YV2", "YV3", "YV4", "YV5", // Volvo (French)
            "VFU", // Renault (old)
            "SUU", // Peugeot Motocycles
            "VF6"  // Citroën (alternative)
        };

        public ValidationResult Validate(string chassis)
        {
            var result = new ValidationResult();
            if (string.IsNullOrWhiteSpace(chassis))
            {
                result.AddError("Chassis", "Cannot be empty");
                return result;
            }

            var normalized = chassis.ToUpperInvariant()
                                    .Replace(" ", "")
                                    .Replace("-", "")
                                    .Replace(".", "");

            if (normalized.Length < 11 || normalized.Length > 17)
                result.AddError("Chassis", "Must be 11-17 characters (VIN standard)");

            if (normalized.Any(c => !char.IsLetterOrDigit(c)))
                result.AddError("Chassis", "Only letters and digits allowed");

            if (normalized.Contains('I') || normalized.Contains('O') || normalized.Contains('Q'))
                result.AddWarning("Chassis", "Contains letters I, O, Q – not valid in VIN");

            if (normalized.Length >= 3)
            {
                var prefix = normalized.Substring(0, 3);
                if (!ValidFrenchPrefixes.Contains(prefix))
                    result.AddWarning("Chassis", $"Non-French manufacturer prefix: {prefix}");
            }

            return result;
        }
    }
}