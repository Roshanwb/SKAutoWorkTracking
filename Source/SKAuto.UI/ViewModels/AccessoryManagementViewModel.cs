using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SKAuto.UI.ViewModels
{
    public partial class AccessoryManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;

        // Fields for commands that need CanExecute updates
        private IRelayCommand<Accessory>? _editAccessoryCommand;
        private IAsyncRelayCommand? _deleteAccessoryCommand;
        private IAsyncRelayCommand? _saveAccessoryCommand;

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

        // Public command properties
        public IAsyncRelayCommand LoadAccessoriesCommand { get; }
        public IRelayCommand AddAccessoryCommand { get; }
        public IRelayCommand<Accessory> EditAccessoryCommand => _editAccessoryCommand!;
        public IAsyncRelayCommand DeleteAccessoryCommand => _deleteAccessoryCommand!;
        public IAsyncRelayCommand SaveAccessoryCommand => _saveAccessoryCommand!;
        public IRelayCommand CancelEditCommand { get; }

        public AccessoryManagementViewModel(IUnitOfWork unitOfWork, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;

            LoadAccessoriesCommand = new AsyncRelayCommand(LoadAccessoriesAsync);
            AddAccessoryCommand = new RelayCommand(AddAccessory);
            _editAccessoryCommand = new RelayCommand<Accessory>(EditAccessory, a => a != null);
            _deleteAccessoryCommand = new AsyncRelayCommand(DeleteAccessoryAsync, () => SelectedAccessory != null);
            _saveAccessoryCommand = new AsyncRelayCommand(SaveAccessoryAsync, () => CurrentEditAccessory != null);
            CancelEditCommand = new RelayCommand(CancelEdit);

            // Load immediately
            LoadAccessoriesCommand.Execute(null);
        }

        partial void OnSelectedAccessoryChanged(Accessory? value)
        {
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
                Time = 30,
                Price = null,
                RequiresPassword = false,
                IsActive = true
            };
            IsEditMode = true;
        }

        private void EditAccessory(Accessory? accessory)
        {
            if (accessory == null) return;

            CurrentEditAccessory = accessory;   // edit directly – changes will be saved from this instance
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
                if (CurrentEditAccessory.Id == 0)
                    await _unitOfWork.Accessories.AddAsync(CurrentEditAccessory);
                else
                    await _unitOfWork.Accessories.UpdateAsync(CurrentEditAccessory); // tracked instance

                await _unitOfWork.CompleteAsync();
                await LoadAccessoriesAsync(); // refresh list
                CancelEdit();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to save accessory", ex);
                MessageBox.Show($"Error saving accessory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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