using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class ClientManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private ObservableCollection<Client> _clients = new();

        [ObservableProperty]
        private Client? _selectedClient;

        public IAsyncRelayCommand LoadClientsCommand { get; }
        public IRelayCommand AddClientCommand { get; }
        public IAsyncRelayCommand EditClientCommand { get; }
        public IAsyncRelayCommand DeleteClientCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public ClientManagementViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            LoadClientsCommand = new AsyncRelayCommand(LoadClientsAsync);
            AddClientCommand = new RelayCommand(AddClient);
            EditClientCommand = new AsyncRelayCommand(EditClientAsync, () => SelectedClient != null);
            DeleteClientCommand = new AsyncRelayCommand(DeleteClientAsync, () => SelectedClient != null);
            CloseCommand = new RelayCommand(CloseWindow);

            LoadClientsCommand.Execute(null);
        }

        private async Task LoadClientsAsync()
        {
            var list = await _unitOfWork.Clients.GetAllAsync();
            Clients = new ObservableCollection<Client>(list.OrderBy(c => c.Name));
        }

        private void AddClient()
        {
            var newClient = new Client { Name = "New Client", IsActive = true };
            var vm = new ClientEditViewModel(_unitOfWork, newClient);
            var win = new ClientEditWindow { DataContext = vm };
            if (win.ShowDialog() == true)
            {
                LoadClientsCommand.Execute(null); // refresh list
            }
        }

        private async Task EditClientAsync()
        {
            if (SelectedClient == null) return;
            // Clone or use the actual entity? We'll pass the selected client directly.
            var vm = new ClientEditViewModel(_unitOfWork, SelectedClient);
            var win = new ClientEditWindow { DataContext = vm };
            if (win.ShowDialog() == true)
            {
                await LoadClientsAsync(); // refresh
            }
        }

        private async Task DeleteClientAsync()
        {
            if (SelectedClient == null) return;
            var result = MessageBox.Show($"Delete client '{SelectedClient.Name}'? This may affect existing work orders.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _unitOfWork.Clients.DeleteAsync(SelectedClient);
                await _unitOfWork.CompleteAsync();
                await LoadClientsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }
        /// <summary>
        ///  This method is called by the source generator whenever the SelectedClient property changes. It updates the CanExecute state of the Edit and Delete commands, enabling or disabling them based on whether a client is selected. --!
        /// </summary>
        /// <param name="value"></param>
        partial void OnSelectedClientChanged(Client? value)
        {
            EditClientCommand.NotifyCanExecuteChanged();
            DeleteClientCommand.NotifyCanExecuteChanged();
        }
    }
}