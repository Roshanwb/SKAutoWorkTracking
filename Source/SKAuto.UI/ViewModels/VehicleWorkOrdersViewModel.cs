using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.Core.Services;
using SKAuto.UI.Views;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class VehicleWorkOrdersViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private string _chassisNumber = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkOrder> _workOrders = new();

        [ObservableProperty]
        private WorkOrder? _selectedWorkOrder;

        public IRelayCommand OpenWorkOrderCommand { get; }

        public VehicleWorkOrdersViewModel(IUnitOfWork unitOfWork, string chassisNumber)
        {
            _unitOfWork = unitOfWork;
            ChassisNumber = chassisNumber;
            OpenWorkOrderCommand = new RelayCommand(OpenWorkOrder, () => SelectedWorkOrder != null);
            _ = LoadWorkOrdersAsync();
        }

        partial void OnSelectedWorkOrderChanged(WorkOrder? value)
        {
            OpenWorkOrderCommand?.NotifyCanExecuteChanged();
        }

        private void OpenWorkOrder()
        {
            if (SelectedWorkOrder == null) return;

            var logger = App.GetService<ILoggingService>();
            var configService = App.GetService<IConfigurationService>();
            var driveService = App.GetService<IGoogleDriveService>();
            var messageBoxService = App.GetService<IMessageBoxService>();

            var detailVM = new WorkOrderDetailViewModel(
                _unitOfWork, logger, configService, driveService, messageBoxService,
                SelectedWorkOrder.Id,
                initialOrderDate: null,
                isReadOnly: true);

            var window = new WorkOrderDetailWindow
            {
                DataContext = detailVM,
                Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                        ?? System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private async Task LoadWorkOrdersAsync()
        {
            if (string.IsNullOrWhiteSpace(ChassisNumber))
                return;

            var vehicle = await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == ChassisNumber);
            if (vehicle == null || !vehicle.Any())
                return;

            var vehicleId = vehicle.First().Id;

            var orders = await _unitOfWork.WorkOrders.FindAsync(wo => wo.VehicleId == vehicleId);
            WorkOrders = new ObservableCollection<WorkOrder>(orders.OrderByDescending(wo => wo.OrderDate));
        }
    }
}