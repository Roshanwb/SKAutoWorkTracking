using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SKAuto.UI.ViewModels
{
    public partial class VehicleManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private ObservableCollection<Vehicle> _vehicles = new();

        [ObservableProperty]
        private Vehicle? _selectedVehicle;

        [ObservableProperty]
        private string _searchText = "";

        private ICollectionView? _filteredVehicles;
        public ICollectionView FilteredVehicles
        {
            get => _filteredVehicles;
            set => SetProperty(ref _filteredVehicles, value);
        }

        public IAsyncRelayCommand LoadVehiclesCommand { get; }
        public IRelayCommand AddVehicleCommand { get; }
        public IAsyncRelayCommand EditVehicleCommand { get; }
        public IAsyncRelayCommand DeleteVehicleCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public VehicleManagementViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            LoadVehiclesCommand = new AsyncRelayCommand(LoadVehiclesAsync);
            AddVehicleCommand = new RelayCommand(AddVehicle);
            EditVehicleCommand = new AsyncRelayCommand(EditVehicleAsync, () => SelectedVehicle != null);
            DeleteVehicleCommand = new AsyncRelayCommand(DeleteVehicleAsync, () => SelectedVehicle != null);
            CloseCommand = new RelayCommand(CloseWindow);

            LoadVehiclesCommand.Execute(null);
        }

        private async Task LoadVehiclesAsync()
        {
            var list = await _unitOfWork.Vehicles.GetAllAsync();
            Vehicles = new ObservableCollection<Vehicle>(list.OrderBy(v => v.ChassisNumber));
            FilteredVehicles = CollectionViewSource.GetDefaultView(Vehicles);
            FilteredVehicles.Filter = VehicleFilter;
            OnPropertyChanged(nameof(FilteredVehicles));
        }

        private bool VehicleFilter(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;
            var vehicle = item as Vehicle;
            if (vehicle == null) return false;
            return vehicle.ChassisNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true;
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredVehicles?.Refresh();
        }

        private void AddVehicle()
        {
            var newVehicle = new Vehicle { ChassisNumber = "NEW", Model = "Unknown", IsActive = true };
            var vm = new VehicleEditViewModel(_unitOfWork, newVehicle);
            var win = new VehicleEditWindow { DataContext = vm };
            if (win.ShowDialog() == true)
            {
                LoadVehiclesCommand.Execute(null);
            }
        }

        private async Task EditVehicleAsync()
        {
            if (SelectedVehicle == null) return;
            var vm = new VehicleEditViewModel(_unitOfWork, SelectedVehicle);
            var win = new VehicleEditWindow { DataContext = vm };
            if (win.ShowDialog() == true)
            {
                await LoadVehiclesAsync();
            }
        }

        private async Task DeleteVehicleAsync()
        {
            if (SelectedVehicle == null) return;
            var result = MessageBox.Show($"Delete vehicle {SelectedVehicle.ChassisNumber}? This may affect existing work orders.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _unitOfWork.Vehicles.DeleteAsync(SelectedVehicle);
                await _unitOfWork.CompleteAsync();
                await LoadVehiclesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting vehicle: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }

        partial void OnSelectedVehicleChanged(Vehicle? value)
        {
            EditVehicleCommand.NotifyCanExecuteChanged();
            DeleteVehicleCommand.NotifyCanExecuteChanged();
        }
    }
}