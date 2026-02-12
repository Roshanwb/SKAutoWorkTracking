using CommunityToolkit.Mvvm.ComponentModel;
using SKAuto.Core.Interfaces;

namespace SKAuto.UI.ViewModels
{
    public partial class WorkOrderViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        public WorkOrderViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public WorkOrderViewModel(IUnitOfWork unitOfWork, int workOrderId) : this(unitOfWork)
        { }
    }
}