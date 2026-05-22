using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Views;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SKAuto.UI.ViewModels
{
    public partial class ClientManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;

        [ObservableProperty]
        private ObservableCollection<Client> _clients = new();

        [ObservableProperty]
        private Client? _selectedClient;

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private ObservableCollection<Client> _selectedClients = new();

        private ICollectionView? _filteredClients;
        public ICollectionView FilteredClients
        {
            get => _filteredClients;
            set => SetProperty(ref _filteredClients, value);
        }

        public IAsyncRelayCommand LoadClientsCommand { get; }
        public IRelayCommand AddClientCommand { get; }
        public IAsyncRelayCommand EditClientCommand { get; }
        public IAsyncRelayCommand DeleteClientCommand { get; }
        public IRelayCommand CloseCommand { get; }
        public IAsyncRelayCommand MergeClientsCommand { get; }

        public ClientManagementViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            LoadClientsCommand = new AsyncRelayCommand(LoadClientsAsync);
            AddClientCommand = new RelayCommand(AddClient);
            EditClientCommand = new AsyncRelayCommand(EditClientAsync, () => SelectedClient != null);
            DeleteClientCommand = new AsyncRelayCommand(DeleteClientAsync, () => SelectedClient != null);
            CloseCommand = new RelayCommand(CloseWindow);
            SelectedClients = new ObservableCollection<Client>();
            MergeClientsCommand = new AsyncRelayCommand(MergeClientsAsync, () => SelectedClients.Count >= 2);

            LoadClientsCommand.Execute(null);
        }

        private async Task LoadClientsAsync()
        {
            var list = await _unitOfWork.Clients.GetAllAsync();
            Clients = new ObservableCollection<Client>(list.OrderBy(c => c.Name));
            FilteredClients = CollectionViewSource.GetDefaultView(Clients);
            FilteredClients.Filter = ClientFilter;
            OnPropertyChanged(nameof(FilteredClients));
        }

        partial void OnSelectedClientsChanged(ObservableCollection<Client> value)
        {
            // ✅ Added null-conditional operator to prevent NullReferenceException
            MergeClientsCommand?.NotifyCanExecuteChanged();
        }

        private bool ClientFilter(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return true;
            var client = item as Client;
            if (client == null) return false;
            return client.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true;
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredClients?.Refresh();
        }

        private void AddClient()
        {
            var newClient = new Client { Name = "New Client", IsActive = true };
            var vm = new ClientEditViewModel(_unitOfWork, newClient);
            var win = new ClientEditWindow { DataContext = vm };
            if (win.ShowDialog() == true)
            {
                LoadClientsCommand.Execute(null);
            }
        }

        private async Task EditClientAsync()
        {
            if (SelectedClient == null) return;
            var vm = new ClientEditViewModel(_unitOfWork, SelectedClient);
            var win = new ClientEditWindow { DataContext = vm };
            if (win.ShowDialog() == true)
            {
                await LoadClientsAsync();
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

        partial void OnSelectedClientChanged(Client? value)
        {
            EditClientCommand.NotifyCanExecuteChanged();
            DeleteClientCommand.NotifyCanExecuteChanged();
        }

        private async Task MergeClientsAsync()
        {
            if (SelectedClients == null || SelectedClients.Count < 2)
            {
                MessageBox.Show("Please select at least two clients to merge.", "Merge Clients", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var master = SelectedClients.OrderBy(c => c.Id).First();
            var others = SelectedClients.Where(c => c.Id != master.Id).ToList();

            var dialog = new ClientMergeDialog(master);
            if (dialog.ShowDialog() != true)
                return;

            // Update master with user-modified values
            master.Name = dialog.ClientName;
            master.Phone = dialog.Phone;
            master.Email = dialog.Email;
            master.Address = dialog.Address;
            master.Notes = dialog.Notes;
            master.IsActive = dialog.IsActive;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Reassign vehicles
                foreach (var client in others)
                {
                    var vehicles = await _unitOfWork.Vehicles.FindAsync(v => v.ClientId == client.Id);
                    foreach (var vehicle in vehicles)
                    {
                        vehicle.ClientId = master.Id;
                        await _unitOfWork.Vehicles.UpdateAsync(vehicle);
                    }
                }
                await _unitOfWork.Clients.UpdateAsync(master);
                foreach (var client in others)
                {
                    await _unitOfWork.Clients.DeleteAsync(client);
                }
                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                MessageBox.Show($"Successfully merged {others.Count} client(s) into '{master.Name}'.", "Merge Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadClientsAsync();
                SelectedClients.Clear();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                MessageBox.Show($"Error merging clients: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}