using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SKAuto.UI.ViewModels
{
    public partial class AccessoryManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;

        [ObservableProperty]
        private ObservableCollection<Accessory> _accessories = new();

        [ObservableProperty]
        private Accessory? _selectedAccessory;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private Accessory? _currentEditAccessory;

        [ObservableProperty]
        private bool _isEditMode;

        // Commands (stored as IRelayCommand to access NotifyCanExecuteChanged)
        private IRelayCommand? _editAccessoryCommand;
        private IRelayCommand? _deleteAccessoryCommand;
        private IRelayCommand? _saveAccessoryCommand;

        public ICommand LoadAccessoriesCommand { get; }
        public ICommand AddAccessoryCommand { get; }
        public ICommand EditAccessoryCommand => _editAccessoryCommand!;
        public ICommand DeleteAccessoryCommand => _deleteAccessoryCommand!;
        public ICommand SaveAccessoryCommand => _saveAccessoryCommand!;
        public ICommand CancelEditCommand { get; }

        public AccessoryManagementViewModel(IUnitOfWork unitOfWork, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;

            LoadAccessoriesCommand = new RelayCommand(async () => await LoadAccessoriesAsync());
            AddAccessoryCommand = new RelayCommand(AddAccessory);
            _editAccessoryCommand = new RelayCommand(EditAccessory, () => SelectedAccessory != null);
            _deleteAccessoryCommand = new RelayCommand(async () => await DeleteAccessoryAsync(), () => SelectedAccessory != null);
            _saveAccessoryCommand = new RelayCommand(async () => await SaveAccessoryAsync(), () => CurrentEditAccessory != null);
            CancelEditCommand = new RelayCommand(CancelEdit);

            // Load immediately
            Task.Run(async () => await LoadAccessoriesAsync());
        }

        partial void OnSelectedAccessoryChanged(Accessory? value)
        {
            // Notify commands that depend on SelectedAccessory
            _editAccessoryCommand?.NotifyCanExecuteChanged();
            _deleteAccessoryCommand?.NotifyCanExecuteChanged();
        }

        partial void OnCurrentEditAccessoryChanged(Accessory? value)
        {
            _saveAccessoryCommand?.NotifyCanExecuteChanged();
        }

        private async Task LoadAccessoriesAsync()
        {
            try
            {
                var accessories = await _unitOfWork.Accessories.GetAllAsync();

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    Accessories.Clear();
                    foreach (var a in accessories.Where(x => x.IsActive).OrderBy(x => x.Name))
                        Accessories.Add(a);
                });

                _logger?.LogInfo($"Loaded {accessories.Count()} accessories");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to load accessories: {ex.Message}");
            }
        }

        private void AddAccessory()
        {
            CurrentEditAccessory = new Accessory
            {
                Name = "",
                PartNumber = "",
                Description = "",
                StandardFittingTime = 30,
                PSAHourlyRate = null,
                SellingPrice = null,
                RequiresPassword = false,
                IsActive = true
            };
            IsEditMode = true;
        }

        private void EditAccessory()
        {
            if (SelectedAccessory == null) return;

            // Clone for editing
            CurrentEditAccessory = new Accessory
            {
                Id = SelectedAccessory.Id,
                PartNumber = SelectedAccessory.PartNumber,
                Name = SelectedAccessory.Name,
                Description = SelectedAccessory.Description,
                StandardFittingTime = SelectedAccessory.StandardFittingTime,
                PSAHourlyRate = SelectedAccessory.PSAHourlyRate,
                SellingPrice = SelectedAccessory.SellingPrice,
                RequiresPassword = SelectedAccessory.RequiresPassword,
                IsActive = SelectedAccessory.IsActive
            };
            IsEditMode = true;
        }

        private async Task DeleteAccessoryAsync()
        {
            if (SelectedAccessory == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Delete accessory '{SelectedAccessory.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    await _unitOfWork.Accessories.DeleteAsync(SelectedAccessory);
                    await _unitOfWork.CompleteAsync();

                    await LoadAccessoriesAsync();
                    SelectedAccessory = null; // Clear selection after delete
                    _logger?.LogInfo($"Deleted accessory: {SelectedAccessory?.Name}");
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Failed to delete: {ex.Message}");
                    System.Windows.MessageBox.Show($"Cannot delete: {ex.Message}", "Error");
                }
            }
        }

        private async Task SaveAccessoryAsync()
        {
            if (CurrentEditAccessory == null) return;

            try
            {
                if (string.IsNullOrWhiteSpace(CurrentEditAccessory.Name))
                {
                    System.Windows.MessageBox.Show("Accessory name is required", "Validation");
                    return;
                }

                if (CurrentEditAccessory.Id == 0)
                {
                    await _unitOfWork.Accessories.AddAsync(CurrentEditAccessory);
                }
                else
                {
                    await _unitOfWork.Accessories.UpdateAsync(CurrentEditAccessory);
                }

                await _unitOfWork.CompleteAsync();
                await LoadAccessoriesAsync();

                IsEditMode = false;
                CurrentEditAccessory = null;
                _logger?.LogInfo($"Saved accessory: {CurrentEditAccessory?.Name}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to save: {ex.Message}");
                System.Windows.MessageBox.Show($"Save failed: {ex.Message}", "Error");
            }
        }

        private void CancelEdit()
        {
            IsEditMode = false;
            CurrentEditAccessory = null;
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterAccessories();
        }

        private void FilterAccessories()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // Reload all
                _ = LoadAccessoriesAsync();
                return;
            }

            var filtered = Accessories.Where(a =>
                (a.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.PartNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

            Accessories.Clear();
            foreach (var a in filtered)
                Accessories.Add(a);
        }
    }
}