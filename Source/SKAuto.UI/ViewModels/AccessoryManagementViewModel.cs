using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Localization;
using System.Collections.ObjectModel;
namespace SKAuto.UI.ViewModels
{
    public partial class AccessoryManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;

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

        // Separate properties for editing (avoids partial method issues)
        [ObservableProperty]
        private string _editName = string.Empty;

        [ObservableProperty]
        private TaskType _editTaskType = TaskType.Fit;

        [ObservableProperty]
        private string _editPartNumber = string.Empty;

        [ObservableProperty]
        private string _editDescription = string.Empty;

        [ObservableProperty]
        private int? _editTime = 30;

        [ObservableProperty]
        private decimal? _editPrice;

        [ObservableProperty]
        private bool _editRequiresPassword;

        [ObservableProperty]
        private bool _editIsActive = true;

        public Array TaskTypeValues => Enum.GetValues(typeof(TaskType));

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

            _logger.LogInfo(LocalizationManager.Instance["AccessoryManagementInitializing"]);

            LoadAccessoriesCommand = new AsyncRelayCommand(LoadAccessoriesAsync);
            AddAccessoryCommand = new RelayCommand(AddAccessory);
            _editAccessoryCommand = new RelayCommand<Accessory>(EditAccessory, a => a != null);
            _deleteAccessoryCommand = new AsyncRelayCommand(DeleteAccessoryAsync, () => SelectedAccessory != null);
            _saveAccessoryCommand = new AsyncRelayCommand(SaveAccessoryAsync, () => !string.IsNullOrWhiteSpace(EditName));
            CancelEditCommand = new RelayCommand(CancelEdit);

            _logger.LogInfo(LocalizationManager.Instance["AccessoryManagementInitializationComplete"]);
            LoadAccessoriesCommand.Execute(null);
        }

        partial void OnSelectedAccessoryChanged(Accessory? value)
        {
            if (value != null)
                _logger.LogInfo($"Selected accessory changed to ID {value.Id}, Name '{value.Name}'");
            else
                _logger.LogInfo(LocalizationManager.Instance["SelectedAccessoryChangedToNull"]);
            _editAccessoryCommand?.NotifyCanExecuteChanged();
            _deleteAccessoryCommand?.NotifyCanExecuteChanged();
        }

        partial void OnCurrentEditAccessoryChanged(Accessory? value)
        {
            if (value != null)
            {
                // Populate edit fields from the accessory
                EditName = value.Name ?? "";
                EditTaskType = value.TaskType;
                EditPartNumber = value.PartNumber ?? "";
                EditDescription = value.Description ?? "";
                EditTime = value.Time;
                EditPrice = value.Price;
                EditRequiresPassword = value.RequiresPassword;
                EditIsActive = value.IsActive;
                _logger.LogInfo($"Edit fields populated for accessory ID {value.Id}");
            }
            else
            {
                // Clear edit fields
                EditName = "";
                EditTaskType = TaskType.Fit;
                EditPartNumber = "";
                EditDescription = "";
                EditTime = 30;
                EditPrice = null;
                EditRequiresPassword = false;
                EditIsActive = true;
                _logger.LogInfo(LocalizationManager.Instance["EditFieldsCleared"]);
            }
            _saveAccessoryCommand?.NotifyCanExecuteChanged();
        }

        partial void OnEditNameChanged(string value)
        {
            _saveAccessoryCommand?.NotifyCanExecuteChanged();
        }

        private async Task LoadAccessoriesAsync()
        {
            _logger.LogInfo(LocalizationManager.Instance["LoadAccessoriesAsyncStarted"]);
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
                _logger.LogError(LocalizationManager.Instance["LoadAccessoriesAsyncFailed"], ex);
                System.Windows.MessageBox.Show($"Error loading accessories: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void AddAccessory()
        {
            _logger.LogInfo(LocalizationManager.Instance["AddAccessoryCalled"]);
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
            _logger.LogInfo(LocalizationManager.Instance["NewAccessoryCreatedEditModeActivated"]);
        }

        private void EditAccessory(Accessory? accessory)
        {
            if (accessory == null)
            {
                _logger.LogWarning(LocalizationManager.Instance["EditAccessoryCalledWithNull"]);
                return;
            }

            _logger.LogInfo($"EditAccessory called for accessory ID {accessory.Id}, Name '{accessory.Name}'");
            CurrentEditAccessory = accessory;
            IsEditMode = true;
            _logger.LogInfo(LocalizationManager.Instance["EditModeActivatedForAccessory"]);
        }

        private async Task DeleteAccessoryAsync()
        {
            if (SelectedAccessory == null)
            {
                _logger.LogWarning(LocalizationManager.Instance["DeleteAccessoryNoAccessorySelected"]);
                return;
            }

            var accessoryToDelete = SelectedAccessory;
            _logger.LogInfo($"DeleteAccessoryAsync called for accessory ID {accessoryToDelete.Id}, Name '{accessoryToDelete.Name}'");

            var result = System.Windows.MessageBox.Show(
                $"Delete accessory '{accessoryToDelete.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
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
                System.Windows.MessageBox.Show($"Cannot delete: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task SaveAccessoryAsync()
        {
            if (CurrentEditAccessory == null)
            {
                _logger.LogWarning(LocalizationManager.Instance["SaveAccessoryNoAccessoryToSave"]);
                return;
            }

            var accessoryToSave = CurrentEditAccessory;
            bool isNew = accessoryToSave.Id == 0;
            string newName = EditName?.Trim() ?? "";
            _logger.LogInfo($"SaveAccessoryAsync called for {(isNew ? "new" : "existing")} accessory: ID {accessoryToSave.Id}, Name '{newName}'");

            if (string.IsNullOrWhiteSpace(newName))
            {
                _logger.LogWarning(LocalizationManager.Instance["SaveAttemptedWithEmptyAccessoryName"]);
                System.Windows.MessageBox.Show(LocalizationManager.Instance["AccessoryNameEmptyError"], "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Duplicate name check
                bool nameChanged = isNew || !string.Equals(accessoryToSave.Name, newName, StringComparison.OrdinalIgnoreCase);
                if (nameChanged)
                {
                    var existing = (await _unitOfWork.Accessories.FindAsync(a => a.Name.ToLower() == newName.ToLower())).FirstOrDefault();
                    if (existing != null && (isNew || existing.Id != accessoryToSave.Id))
                    {
                        _logger.LogWarning($"Duplicate accessory name '{newName}' – existing ID {existing.Id}");
                        System.Windows.MessageBox.Show($"An accessory with the name '{newName}' already exists. Please use a different name.", "Duplicate Name", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                }

                // Update accessory with edited values
                accessoryToSave.Name = newName;
                accessoryToSave.TaskType = EditTaskType;
                accessoryToSave.PartNumber = EditPartNumber;
                accessoryToSave.Description = EditDescription;
                accessoryToSave.Time = EditTime;
                accessoryToSave.Price = EditPrice;
                accessoryToSave.RequiresPassword = EditRequiresPassword;
                accessoryToSave.IsActive = EditIsActive;

                if (isNew)
                {
                    _logger.LogInfo($"Adding new accessory with Name '{newName}'");
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
                System.Windows.MessageBox.Show($"Error saving accessory: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CancelEdit()
        {
            _logger.LogInfo(LocalizationManager.Instance["CancelEditCalled"]);
            IsEditMode = false;
            CurrentEditAccessory = null;
            _logger.LogInfo(LocalizationManager.Instance["EditModeCancelled"]);
        }

        partial void OnSearchTextChanged(string value)
        {
            _logger.LogInfo($"Search text changed: '{value}'");
            FilterAccessories();
        }

        private void FilterAccessories()
        {
            _logger.LogInfo(LocalizationManager.Instance["FilterAccessoriesStarted"]);

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _logger.LogInfo(LocalizationManager.Instance["SearchTextEmptyReloadingAccessories"]);
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