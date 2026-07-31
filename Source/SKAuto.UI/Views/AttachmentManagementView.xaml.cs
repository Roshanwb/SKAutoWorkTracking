using SKAuto.UI.ViewModels;
using System.Windows;

namespace SKAuto.UI.Views
{
    public partial class AttachmentManagementView : Window
    {
        public AttachmentManagementView(AttachmentManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}