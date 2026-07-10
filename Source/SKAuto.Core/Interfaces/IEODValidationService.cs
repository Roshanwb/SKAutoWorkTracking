namespace SKAuto.Core.Interfaces
{
    public interface IEODValidationService
    {
        Task<ValidationResult> ValidateDayAsync(DateTime date);
    }
}