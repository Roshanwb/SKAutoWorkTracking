using CommunityToolkit.Mvvm.ComponentModel;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SKAuto.UI.ViewModels
{
    public partial class VehicleWorkOrdersViewModel : ObservableObject  // ← added 'partial'
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private string _chassisNumber = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkOrder> _workOrders = new();

        public VehicleWorkOrdersViewModel(IUnitOfWork unitOfWork, string chassisNumber)
        {
            _unitOfWork = unitOfWork;
            ChassisNumber = chassisNumber;
            _ = LoadWorkOrdersAsync();
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