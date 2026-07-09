using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using System;
using System.Threading.Tasks;
using System.Windows;

using SKAuto.UI.Localization;
namespace SKAuto.UI.ViewModels
{
    public partial class VehicleEditViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly bool _isNew;

        [ObservableProperty]
        private Vehicle _vehicle;

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public VehicleEditViewModel(IUnitOfWork unitOfWork, Vehicle? vehicle = null)
        {
            _unitOfWork = unitOfWork;
            _isNew = vehicle == null;
            _vehicle = vehicle ?? new Vehicle { IsActive = true };

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(CloseWindow);
        }

        private void CloseWindow()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(Vehicle.ChassisNumber))
                {
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["ChassisNumberRequired"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (_isNew)
                    await _unitOfWork.Vehicles.AddAsync(Vehicle);
                else
                    await _unitOfWork.Vehicles.UpdateAsync(Vehicle);

                await _unitOfWork.CompleteAsync();
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving vehicle: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CloseWindow(bool success = false)
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.DialogResult = success;
                    window.Close();
                    break;
                }
        }
    }
}