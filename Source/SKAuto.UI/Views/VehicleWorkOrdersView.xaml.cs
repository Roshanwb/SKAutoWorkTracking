using SKAuto.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SKAuto.UI.Views
{
    public partial class VehicleWorkOrdersView : Window
    {
        public VehicleWorkOrdersView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is DataGrid dataGrid)) return;
            if (dataGrid.SelectedItem == null) return;

            // Ignore double-clicks outside a row (e.g. header, empty space)
            if (e.OriginalSource is DependencyObject source)
            {
                var row = ItemsControl.ContainerFromElement(dataGrid, source) as DataGridRow;
                if (row == null) return;
            }

            if (DataContext is VehicleWorkOrdersViewModel vm)
            {
                vm.OpenWorkOrderCommand.Execute(null);
            }
        }
    }
}