using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class PriceUpdateViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;

        [ObservableProperty]
        private ObservableCollection<Accessory> _accessories = new();

        [ObservableProperty]
        private Accessory? _selectedAccessory;

        [ObservableProperty]
        private DateTime? _fromDate = DateTime.Today.AddMonths(-1);

        [ObservableProperty]
        private DateTime? _toDate = DateTime.Today;

        [ObservableProperty]
        private string _newPrice = "";   // empty initially

        [ObservableProperty]
        private bool _updateDefaultPrice = true;

        [ObservableProperty]
        private string _statusMessage = "";

        public IAsyncRelayCommand UpdateCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public PriceUpdateViewModel(IUnitOfWork unitOfWork, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;

            UpdateCommand = new AsyncRelayCommand(UpdatePricesAsync, CanUpdate);
            CloseCommand = new RelayCommand(() => CloseWindow(false));

            // Load accessories asynchronously; the command will be re‑evaluated when SelectedAccessory changes
            _ = LoadAccessoriesAsync();
        }

        private async Task LoadAccessoriesAsync()
        {
            try
            {
                var all = await _unitOfWork.Accessories.GetAllAsync();
                Accessories = new ObservableCollection<Accessory>(all.OrderBy(a => a.Name));
                if (Accessories.Any())
                    SelectedAccessory = Accessories.First();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load accessories for bulk price update", ex);
                StatusMessage = $"Error loading accessories: {ex.Message}";
            }
        }

        private bool CanUpdate()
        {
            // All conditions must be true:
            // - an accessory is selected
            // - both dates are set (they are by default)
            // - from <= to
            // - NewPrice is not empty and is a valid decimal
            if (SelectedAccessory == null) return false;
            if (!FromDate.HasValue || !ToDate.HasValue) return false;
            if (FromDate > ToDate) return false;
            if (string.IsNullOrWhiteSpace(NewPrice)) return false;
            if (!decimal.TryParse(NewPrice, out _)) return false;
            return true;
        }

        // Refresh the command whenever any of the relevant properties change
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.PropertyName == nameof(SelectedAccessory) ||
                e.PropertyName == nameof(FromDate) ||
                e.PropertyName == nameof(ToDate) ||
                e.PropertyName == nameof(NewPrice))
            {
                UpdateCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task UpdatePricesAsync()
        {
            if (!CanUpdate()) return;

            decimal newPriceValue = decimal.Parse(NewPrice); // safe because CanUpdate validated it
            if (newPriceValue < 0)
            {
                StatusMessage = "Price cannot be negative.";
                return;
            }

            try
            {
                StatusMessage = "Updating work order tasks...";
                _logger.LogInfo($"Bulk price update started: Accessory '{SelectedAccessory!.Name}', From {FromDate:yyyy-MM-dd} To {ToDate:yyyy-MM-dd}, New Price {newPriceValue}");

                var tasks = await _unitOfWork.WorkTasks.FindAsync(t =>
                    t.AccessoryId == SelectedAccessory.Id &&
                    t.WorkOrder.OrderDate >= FromDate.Value &&
                    t.WorkOrder.OrderDate <= ToDate.Value);

                var taskList = tasks.ToList();
                if (!taskList.Any())
                {
                    StatusMessage = "No work orders found for the selected accessory and date range.";
                    return;
                }

                int updatedCount = 0;
                foreach (var task in taskList)
                {
                    task.Price = newPriceValue;
                    await _unitOfWork.WorkTasks.UpdateAsync(task);
                    updatedCount++;
                }

                if (UpdateDefaultPrice)
                {
                    SelectedAccessory.Price = newPriceValue;
                    await _unitOfWork.Accessories.UpdateAsync(SelectedAccessory);
                }


                var affectedWorkOrderIds = taskList.Select(t => t.WorkOrderId).Distinct();
                foreach (var woId in affectedWorkOrderIds)
                {
                    var workOrder = await _unitOfWork.WorkOrders.GetByIdAsync(woId);
                    if (workOrder != null)
                    {
                        var tasksForOrder = await _unitOfWork.WorkTasks.FindAsync(t => t.WorkOrderId == woId);
                        workOrder.TotalAmount = tasksForOrder.Sum(t => (t.Price ?? 0) * t.Quantity);
                        await _unitOfWork.WorkOrders.UpdateAsync(workOrder);
                    }
                }
                await _unitOfWork.CompleteAsync();

                StatusMessage = $"✅ Updated {updatedCount} work order task(s). Accessory default price {(UpdateDefaultPrice ? "updated" : "unchanged")}.";
                _logger.LogInfo($"Bulk price update completed: {updatedCount} tasks updated.");

                MessageBox.Show($"Successfully updated {updatedCount} work order tasks.\nAccessory default price {(UpdateDefaultPrice ? "was also updated." : "remains unchanged.")}",
                                "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                _logger.LogError("Bulk price update failed", ex);
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to update prices: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseWindow(false);
            }
        }

        private void CloseWindow(bool? dialogResult = null)
        {
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.DialogResult = dialogResult;
                    window.Close();
                    break;
                }
        }

    }
}