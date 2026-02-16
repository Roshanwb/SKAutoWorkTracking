using System;
using System.Threading.Tasks;  
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;

namespace SKAuto.UI.ViewModels
{
    public partial class WorkOrderDetailViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private WorkOrder _workOrder;

        [ObservableProperty]
        private ObservableCollection<Accessory> _availableAccessories = new();

        [ObservableProperty]
        private Accessory? _selectedAccessory;

        [ObservableProperty]
        private int _quantity = 1;

        [ObservableProperty]
        private ObservableCollection<WorkTask> _tasks = new();

        public IRelayCommand AddTaskCommand { get; }
        public IRelayCommand<WorkTask> RemoveTaskCommand { get; }
        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public WorkOrderDetailViewModel(IUnitOfWork unitOfWork, WorkOrder workOrder)
        {
            _unitOfWork = unitOfWork;
            _workOrder = workOrder;

            AddTaskCommand = new RelayCommand(AddTask);
            RemoveTaskCommand = new RelayCommand<WorkTask>(RemoveTask);
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(CloseWindow);

            LoadDataAsync().ConfigureAwait(false);
        }

        private void CloseWindow()
        {
            // Find and close the window
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = true;
                    window.Close();
                    break;
                }
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var accessories = await _unitOfWork.Accessories.GetAllAsync();
                AvailableAccessories = new ObservableCollection<Accessory>(accessories);
                Tasks = new ObservableCollection<WorkTask>(WorkOrder.WorkTasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddTask()
        {
            if (SelectedAccessory == null) return;

            var task = new WorkTask
            {
                WorkOrderId = WorkOrder.Id,
                AccessoryId = SelectedAccessory.Id,
                Accessory = SelectedAccessory,
                Quantity = Quantity,
                TaskType = TaskType.Fit,
                TaskStatus = WorkStatus.Planned,
                UnitPrice = SelectedAccessory.SellingPrice,
                EstimatedMinutes = SelectedAccessory.StandardFittingTime
            };

            Tasks.Add(task);
            WorkOrder.WorkTasks.Add(task);
        }

        private void RemoveTask(WorkTask? task)
        {
            if (task != null)
            {
                Tasks.Remove(task);
                WorkOrder.WorkTasks.Remove(task);
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                // Recalculate total
                WorkOrder.CalculateTotal();

                await _unitOfWork.CompleteAsync();
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving work order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow(bool success = false)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = success;
                    window.Close();
                    break;
                }
            }
        }
    }
}