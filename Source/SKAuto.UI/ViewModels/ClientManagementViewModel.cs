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
        private readonly ILoggingService _logger;

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

        public ClientManagementViewModel(IUnitOfWork unitOfWork, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;

            _logger.LogInfo("ClientManagementViewModel initializing");

            LoadClientsCommand = new AsyncRelayCommand(LoadClientsAsync);
            AddClientCommand = new RelayCommand(AddClient);
            EditClientCommand = new AsyncRelayCommand(EditClientAsync, () => SelectedClient != null);
            DeleteClientCommand = new AsyncRelayCommand(DeleteClientAsync, () => SelectedClient != null);
            CloseCommand = new RelayCommand(CloseWindow);
            SelectedClients = new ObservableCollection<Client>();
            MergeClientsCommand = new AsyncRelayCommand(MergeClientsAsync, () => SelectedClients.Count >= 2);

            _logger.LogInfo("ClientManagementViewModel initialization complete, loading clients");
            LoadClientsCommand.Execute(null);
        }

        private async Task LoadClientsAsync()
        {
            _logger.LogInfo("LoadClientsAsync started");
            try
            {
                var list = await _unitOfWork.Clients.GetAllAsync();
                Clients = new ObservableCollection<Client>(list.OrderBy(c => c.Name));
                FilteredClients = CollectionViewSource.GetDefaultView(Clients);
                FilteredClients.Filter = ClientFilter;
                OnPropertyChanged(nameof(FilteredClients));
                _logger.LogInfo($"LoadClientsAsync completed: {Clients.Count} clients loaded");
            }
            catch (Exception ex)
            {
                _logger.LogError("LoadClientsAsync failed", ex);
                throw;
            }
        }

        partial void OnSelectedClientsChanged(ObservableCollection<Client> value)
        {
            MergeClientsCommand?.NotifyCanExecuteChanged();
            _logger.LogInfo($"Selected clients changed, count: {value?.Count ?? 0}");
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
            _logger.LogInfo($"Search text changed: '{value}'");
            FilteredClients?.Refresh();
        }

        private void AddClient()
        {
            _logger.LogInfo("AddClient started");
            try
            {
                var newClient = new Client { Name = "New Client", IsActive = true };
                var vm = new ClientEditViewModel(_unitOfWork, newClient);
                var win = new ClientEditWindow { DataContext = vm };

                _logger.LogInfo("Opening ClientEditWindow for new client");
                if (win.ShowDialog() == true)
                {
                    _logger.LogInfo("Client added successfully, refreshing list");
                    LoadClientsCommand.Execute(null);
                }
                else
                {
                    _logger.LogInfo("Client addition cancelled by user");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("AddClient failed", ex);
                MessageBox.Show($"Error adding client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EditClientAsync()
        {
            if (SelectedClient == null)
            {
                _logger.LogWarning("EditClientAsync called with no client selected");
                return;
            }

            _logger.LogInfo($"EditClientAsync started for client ID {SelectedClient.Id}, Name '{SelectedClient.Name}'");
            try
            {
                var vm = new ClientEditViewModel(_unitOfWork, SelectedClient);
                var win = new ClientEditWindow { DataContext = vm };

                if (win.ShowDialog() == true)
                {
                    _logger.LogInfo($"Client ID {SelectedClient.Id} updated successfully, refreshing list");
                    await LoadClientsAsync();
                }
                else
                {
                    _logger.LogInfo($"Edit cancelled for client ID {SelectedClient.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"EditClientAsync failed for client ID {SelectedClient?.Id}", ex);
                MessageBox.Show($"Error editing client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteClientAsync()
        {
            if (SelectedClient == null)
            {
                _logger.LogWarning("DeleteClientAsync called with no client selected");
                return;
            }

            _logger.LogInfo($"DeleteClientAsync started for client ID {SelectedClient.Id}, Name '{SelectedClient.Name}'");

            var result = MessageBox.Show($"Delete client '{SelectedClient.Name}'? This may affect existing work orders.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                _logger.LogInfo($"Deletion cancelled for client ID {SelectedClient.Id}");
                return;
            }

            try
            {
                await _unitOfWork.Clients.DeleteAsync(SelectedClient);
                await _unitOfWork.CompleteAsync();
                _logger.LogInfo($"Client ID {SelectedClient.Id} deleted successfully");
                await LoadClientsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"DeleteClientAsync failed for client ID {SelectedClient?.Id}", ex);
                MessageBox.Show($"Error deleting client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            _logger.LogInfo("CloseWindow called");
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }

        partial void OnSelectedClientChanged(Client? value)
        {
            if (value != null)
                _logger.LogInfo($"Selected client changed to ID {value.Id}, Name '{value.Name}'");
            else
                _logger.LogInfo("Selected client changed to null");

            EditClientCommand.NotifyCanExecuteChanged();
            DeleteClientCommand.NotifyCanExecuteChanged();
        }

        private async Task MergeClientsAsync()
        {
            _logger.LogInfo("MergeClientsAsync started");

            if (SelectedClients == null || SelectedClients.Count < 2)
            {
                _logger.LogWarning($"MergeClientsAsync called with insufficient selection: {SelectedClients?.Count ?? 0} clients selected");
                MessageBox.Show("Please select at least two clients to merge.", "Merge Clients", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var master = SelectedClients.OrderBy(c => c.Id).First();
            var others = SelectedClients.Where(c => c.Id != master.Id).ToList();

            _logger.LogInfo($"Merge: Master client ID {master.Id} '{master.Name}', will merge {others.Count} other client(s): {string.Join(", ", others.Select(c => $"{c.Id}:'{c.Name}'"))}");

            var dialog = new ClientMergeDialog(master);
            _logger.LogInfo("Opening ClientMergeDialog");

            if (dialog.ShowDialog() != true)
            {
                _logger.LogInfo("Merge cancelled by user");
                return;
            }

            // Store the new values from dialog (will apply after deletion)
            string newName = dialog.ClientName;
            string newPhone = dialog.Phone;
            string newEmail = dialog.Email;
            string newAddress = dialog.Address;
            string newNotes = dialog.Notes;
            bool newIsActive = dialog.IsActive;

            _logger.LogInfo($"Merge dialog result: Name='{newName}', Phone='{newPhone}', Email='{newEmail}', IsActive={newIsActive}");

            await _unitOfWork.BeginTransactionAsync();
            _logger.LogInfo("Transaction started for merge operation");

            try
            {
                // 1. Reassign vehicles from other clients to master
                int vehicleCount = 0;
                foreach (var client in others)
                {
                    var vehicles = await _unitOfWork.Vehicles.FindAsync(v => v.ClientId == client.Id);
                    vehicleCount += vehicles.Count();
                    _logger.LogInfo($"Reassigning {vehicles.Count()} vehicles from client ID {client.Id} to master ID {master.Id}");

                    foreach (var vehicle in vehicles)
                    {
                        vehicle.ClientId = master.Id;
                        await _unitOfWork.Vehicles.UpdateAsync(vehicle);
                    }
                }
                _logger.LogInfo($"Total vehicles reassigned: {vehicleCount}");

                // 2. Delete the other clients (no FK references left because vehicles are now pointing to master)
                foreach (var client in others)
                {
                    _logger.LogInfo($"Deleting client ID {client.Id} '{client.Name}'");
                    await _unitOfWork.Clients.DeleteAsync(client);
                }

                // 3. Now update the master client with the new values (no duplicate name exists)
                master.Name = newName;
                master.Phone = newPhone;
                master.Email = newEmail;
                master.Address = newAddress;
                master.Notes = newNotes;
                master.IsActive = newIsActive;
                await _unitOfWork.Clients.UpdateAsync(master);
                _logger.LogInfo($"Master client ID {master.Id} updated to Name='{newName}'");

                await _unitOfWork.CompleteAsync();
                await _unitOfWork.CommitTransactionAsync();

                _logger.LogInfo($"Merge completed successfully: {others.Count} clients merged into '{newName}' (ID {master.Id})");
                MessageBox.Show($"Successfully merged {others.Count} client(s) into '{newName}'.", "Merge Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadClientsAsync();
                SelectedClients.Clear();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError($"MergeClientsAsync failed: {ex.Message}", ex);
                MessageBox.Show($"Error merging clients: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}