using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Spreadsheet;
using SKAuto.Core.Entities;
using SKAuto.Core.Interfaces;
using SKAuto.UI.Views;
using System;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows;

using SKAuto.UI.Localization;
namespace SKAuto.UI.ViewModels
{
    public partial class UserManagementViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;
        private readonly User _currentUser;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        [ObservableProperty]
        private User? _selectedUser;

        public IAsyncRelayCommand LoadUsersCommand { get; }
        public IRelayCommand AddUserCommand { get; }
        public IAsyncRelayCommand EditUserCommand { get; }
        public IAsyncRelayCommand DeleteUserCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public UserManagementViewModel(IUnitOfWork unitOfWork, ILoggingService logger, User currentUser)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _currentUser = currentUser;

            LoadUsersCommand = new AsyncRelayCommand(LoadUsersAsync);
            AddUserCommand = new RelayCommand(AddUser);
            EditUserCommand = new AsyncRelayCommand(EditUserAsync, () => SelectedUser != null);
            DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync, () => SelectedUser != null);
            CloseCommand = new RelayCommand(() => CloseWindow());

            LoadUsersCommand.Execute(null);
        }

        private async Task LoadUsersAsync()
        {
            try
            {
                var list = await _unitOfWork.Users.GetAllAsync();
                Users = new ObservableCollection<User>(list.OrderBy(u => u.Username));
                _logger.LogInfo($"Loaded {Users.Count} users.");
            }
            catch (Exception ex)
            {
                _logger.LogError(LocalizationManager.Instance["FailedToLoadUsers"], ex);
                System.Windows.MessageBox.Show($"Error loading users: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void AddUser()
        {
            _logger.LogInfo(LocalizationManager.Instance["AddUserCalled"]);
            var vm = new UserEditViewModel(_unitOfWork, _logger, _currentUser, null, UserEditMode.Add);
            var win = new UserEditView(vm);
            if (win.ShowDialog() == true)
            {
                LoadUsersCommand.Execute(null);
            }
        }

        private async Task EditUserAsync()
        {
            if (SelectedUser == null) return;
            _logger.LogInfo($"EditUser called for user {SelectedUser.Username}");
            var vm = new UserEditViewModel(_unitOfWork, _logger, _currentUser, SelectedUser, UserEditMode.Edit);
            var win = new UserEditView(vm);
            if (win.ShowDialog() == true)
            {
                await LoadUsersAsync();
            }
        }

        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;
            if (SelectedUser.Id == _currentUser.Id)
            {
                System.Windows.MessageBox.Show(LocalizationManager.Instance["CannotDeleteOwnAccount"], LocalizationManager.Instance["ClientManagementView_Delete"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            var result = System.Windows.MessageBox.Show($"Delete user '{SelectedUser.Username}'? This action cannot be undone.", "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await _unitOfWork.Users.DeleteAsync(SelectedUser);
                await _unitOfWork.CompleteAsync();
                _logger.LogInfo($"User {SelectedUser.Username} deleted.");
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete user {SelectedUser.Username}", ex);
                System.Windows.MessageBox.Show($"Error deleting user: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CloseWindow()
        {
            foreach (Window w in System.Windows.Application.Current.Windows)
                if (w.DataContext == this)
                {
                    w.Close();
                    break;
                }
        }

        partial void OnSelectedUserChanged(User? value)
        {
            EditUserCommand.NotifyCanExecuteChanged();
            DeleteUserCommand.NotifyCanExecuteChanged();
        }
    }
}
