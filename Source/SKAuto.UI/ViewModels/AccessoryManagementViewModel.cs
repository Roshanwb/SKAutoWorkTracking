using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
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
        public Array TaskTypeValues => Enum.GetValues(typeof(TaskType));

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

            _logger.LogInfo("AccessoryManagementViewModel initializing");

            LoadAccessoriesCommand = new AsyncRelayCommand(LoadAccessoriesAsync);
            AddAccessoryCommand = new RelayCommand(AddAccessory);
            _editAccessoryCommand = new RelayCommand<Accessory>(EditAccessory, a => a != null);
            _deleteAccessoryCommand = new AsyncRelayCommand(DeleteAccessoryAsync, () => SelectedAccessory != null);
            _saveAccessoryCommand = new AsyncRelayCommand(SaveAccessoryAsync, () => CurrentEditAccessory != null);
            CancelEditCommand = new RelayCommand(CancelEdit);

            _logger.LogInfo("AccessoryManagementViewModel initialization complete, loading accessories");
            LoadAccessoriesCommand.Execute(null);
        }

        partial void OnSelectedAccessoryChanged(Accessory? value)
        {
            if (value != null)
                _logger.LogInfo($"Selected accessory changed to ID {value.Id}, Name '{value.Name}'");
            else
                _logger.LogInfo("Selected accessory changed to null");
            _editAccessoryCommand?.NotifyCanExecuteChanged();
            _deleteAccessoryCommand?.NotifyCanExecuteChanged();
        }

        partial void OnCurrentEditAccessoryChanged(Accessory? value)
        {
            if (value != null)
                _logger.LogInfo($"Current edit accessory set to ID {value.Id}, Name '{value.Name}'");
            else
                _logger.LogInfo("Current edit accessory cleared");
            _saveAccessoryCommand?.NotifyCanExecuteChanged();
        }

        private async Task LoadAccessoriesAsync()
        {
            _logger.LogInfo("LoadAccessoriesAsync started");
            try
            {
                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                var activeAccessories = accessories.Where(x => x.IsActive).OrderBy(x => x.Name).ToList();

                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    Accessories.Clear();
                    foreach (var a in activeAccessories)
                        Accessories.Add(a);
                });

                _logger.LogInfo($"LoadAccessoriesAsync completed: {Accessories.Count} active accessories loaded (total {accessories.Count()})");
            }
            catch (Exception ex)
            {
                _logger.LogError("LoadAccessoriesAsync failed", ex);
                MessageBox.Show($"Error loading accessories: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddAccessory()
        {
            _logger.LogInfo("AddAccessory called - creating new accessory");
            CurrentEditAccessory = new Accessory
            {
                Name = "",
                PartNumber = "",
                Description = "",
                Time = 30,
                Price = null,
                RequiresPassword = false,
                IsActive = true,
                TaskType = TaskType.Fit      
            };
            IsEditMode = true;
            _logger.LogInfo("New accessory created, edit mode activated");
        }

        private void EditAccessory(Accessory? accessory)
        {
            if (accessory == null)
            {
                _logger.LogWarning("EditAccessory called with null accessory");
                return;
            }

            _logger.LogInfo($"EditAccessory called for accessory ID {accessory.Id}, Name '{accessory.Name}'");
            CurrentEditAccessory = accessory;
            IsEditMode = true;
            _logger.LogInfo("Edit mode activated for accessory");
        }

        private async Task DeleteAccessoryAsync()
        {
            if (SelectedAccessory == null)
            {
                _logger.LogWarning("DeleteAccessoryAsync called with no accessory selected");
                return;
            }

            var accessoryToDelete = SelectedAccessory;
            _logger.LogInfo($"DeleteAccessoryAsync called for accessory ID {accessoryToDelete.Id}, Name '{accessoryToDelete.Name}'");

            var result = MessageBox.Show(
                $"Delete accessory '{accessoryToDelete.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                _logger.LogInfo($"Deletion cancelled for accessory ID {accessoryToDelete.Id}");
                return;
            }

            try
            {
                await _unitOfWork.Accessories.DeleteAsync(accessoryToDelete);
                await _unitOfWork.CompleteAsync();
                _logger.LogInfo($"Accessory ID {accessoryToDelete.Id} deleted successfully");

                await LoadAccessoriesAsync();
                SelectedAccessory = null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete accessory ID {accessoryToDelete.Id}", ex);
                MessageBox.Show($"Cannot delete: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveAccessoryAsync()
        {
            if (CurrentEditAccessory == null)
            {
                _logger.LogWarning("SaveAccessoryAsync called with no accessory to save");
                return;
            }

            var accessoryToSave = CurrentEditAccessory;
            bool isNew = accessoryToSave.Id == 0;
            _logger.LogInfo($"SaveAccessoryAsync called for {(isNew ? "new" : "existing")} accessory: ID {accessoryToSave.Id}, Name '{accessoryToSave.Name}'");

            try
            {
                if (isNew)
                {
                    _logger.LogInfo($"Adding new accessory with Name '{accessoryToSave.Name}'");
                    await _unitOfWork.Accessories.AddAsync(accessoryToSave);
                }
                else
                {
                    _logger.LogInfo($"Updating existing accessory ID {accessoryToSave.Id}");
                    await _unitOfWork.Accessories.UpdateAsync(accessoryToSave);
                }

                await _unitOfWork.CompleteAsync();
                _logger.LogInfo($"Accessory saved successfully (ID {accessoryToSave.Id})");

                await LoadAccessoriesAsync();
                CancelEdit();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save accessory ID {accessoryToSave.Id}", ex);
                MessageBox.Show($"Error saving accessory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelEdit()
        {
            _logger.LogInfo("CancelEdit called - clearing edit mode");
            IsEditMode = false;
            CurrentEditAccessory = null;
            _logger.LogInfo("Edit mode cancelled");
        }

        partial void OnSearchTextChanged(string value)
        {
            _logger.LogInfo($"Search text changed: '{value}'");
            FilterAccessories();
        }

        private void FilterAccessories()
        {
            _logger.LogInfo("FilterAccessories started");

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _logger.LogInfo("Search text empty, reloading all accessories");
                _ = LoadAccessoriesAsync();
                return;
            }

            var filtered = Accessories.Where(a =>
                (a.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.PartNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

            _logger.LogInfo($"Filtered accessories: {filtered.Count} matches out of {Accessories.Count} total");

            Accessories.Clear();
            foreach (var a in filtered)
                Accessories.Add(a);
        }
    }
}