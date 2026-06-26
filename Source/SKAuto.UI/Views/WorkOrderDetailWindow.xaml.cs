using SKAuto.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SKAuto.UI.Views
{
    public partial class WorkOrderDetailWindow : Window
    {
        public WorkOrderDetailWindow()
        {
            InitializeComponent();
            this.ChassisTextBox.Focus();
        }
        private void ChassisTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is WorkOrderDetailViewModel vm)
                {
                    vm.SearchVehicleCommand.Execute(null);
                }
            }
        }
    }
}