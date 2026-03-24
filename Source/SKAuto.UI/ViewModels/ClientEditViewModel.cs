using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public partial class ClientEditViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        public Array ClientTypes => Enum.GetValues(typeof(ClientType)); 

        [ObservableProperty]
        private Client _client;

        public IAsyncRelayCommand SaveCommand { get; }  // Changed from ICommand to IAsyncRelayCommand
        public IRelayCommand CancelCommand { get; }     // Changed from ICommand to IRelayCommand

        public ClientEditViewModel(IUnitOfWork unitOfWork, Client client)
        {
            _unitOfWork = unitOfWork;
            _client = client;

            // Use AsyncRelayCommand for async methods, RelayCommand for sync methods
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(CloseWindow);
        }

        private void CloseWindow()
        {
            // Find and close the window
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = true;
                    window.Close();
                    break;
                }
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                await _unitOfWork.Clients.UpdateAsync(Client);
                await _unitOfWork.CompleteAsync();
                CloseWindow(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving client: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow(bool success = false)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.DialogResult = success;
                    window.Close();
                    break;
                }
            }
        }
    }
}