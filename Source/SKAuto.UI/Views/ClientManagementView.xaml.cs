using System.Windows;
using System.Windows.Controls;
using SKAuto.Core.Entities;
using SKAuto.UI.ViewModels;

namespace SKAuto.UI.Views
{
    public partial class ClientManagementView : Window
    {
        public ClientManagementView()
        {
            InitializeComponent();
        }

        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is ClientManagementViewModel vm && sender is DataGrid grid)
            {
                vm.SelectedClients.Clear();
                foreach (Client client in grid.SelectedItems)
                {
                    vm.SelectedClients.Add(client);
                }
                // Notify command that CanExecute state may have changed
                vm.MergeClientsCommand.NotifyCanExecuteChanged();
            }
        }
    }
}