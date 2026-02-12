using CommunityToolkit.Mvvm.ComponentModel;
using SKAuto.Core.Interfaces;

namespace SKAuto.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        public MainViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
    }
}