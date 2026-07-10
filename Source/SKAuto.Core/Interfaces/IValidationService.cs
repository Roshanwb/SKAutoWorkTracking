using SKAuto.Core.Entities;

namespace SKAuto.Core.Interfaces
{
    public interface IValidationService
    {
        Task<ValidationResult> ValidateWorkOrderAsync(WorkOrder workOrder);
        Task<ValidationResult> ValidateVehicleAsync(Vehicle vehicle);
        Task<ValidationResult> ValidateChassisNumberAsync(string chassisNumber);
        Task<ValidationResult> ValidateEODDataAsync(DateTime date);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationError> Errors { get; set; } = new();
        public List<ValidationWarning> Warnings { get; set; } = new();

        public void AddError(string property, string message)
        {
            Errors.Add(new ValidationError(property, message));
            IsValid = false;
        }

        public void AddWarning(string property, string message)
        {
            Warnings.Add(new ValidationWarning(property, message));
        }
    }

    public class ValidationError
    {
        public string Property { get; }
        public string Message { get; }

        public ValidationError(string property, string message)
        {
            Property = property;
            Message = message;
        }
    }

    public class ValidationWarning
    {
        public string Property { get; }
        public string Message { get; }

        public ValidationWarning(string property, string message)
        {
            Property = property;
            Message = message;
        }
    }
}
