using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentFormat.OpenXml.Wordprocessing;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Helpers;
using SKAuto.Core.Interfaces;
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public enum UserEditMode
    {
        Add,
        Edit,
        ChangePassword
    }

    public partial class UserEditViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;
        private readonly User _currentUser;
        private readonly User _editingUser;
        private readonly UserEditMode _mode;

        [ObservableProperty]
        private string _windowTitle;

        [ObservableProperty]
        private string _username;

        [ObservableProperty]
        private bool _isUsernameEditable = true;

        [ObservableProperty]
        private string _passwordLabel = "Password";

        [ObservableProperty]
        private string _confirmPasswordLabel = "Confirm Password";

        [ObservableProperty]
        private bool _showConfirmPassword = true;

        [ObservableProperty]
        private bool _showAdminFields;

        [ObservableProperty]
        private UserRole _selectedRole;

        [ObservableProperty]
        private bool _isActive = true;

        // For password fields (bound in code-behind)
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }

        public Array RoleValues => Enum.GetValues(typeof(UserRole));

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public event EventHandler<bool> Completed;

        public UserEditViewModel(IUnitOfWork unitOfWork, ILoggingService logger, User currentUser, User editingUser = null, UserEditMode mode = UserEditMode.Edit)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _currentUser = currentUser;
            _editingUser = editingUser;
            _mode = mode;

            switch (_mode)
            {
                case UserEditMode.Add:
                    WindowTitle = "Add New User";
                    IsUsernameEditable = true;
                    Username = "";
                    PasswordLabel = "Password";
                    ConfirmPasswordLabel = "Confirm Password";
                    ShowConfirmPassword = true;
                    ShowAdminFields = true;
                    IsActive = true;
                    break;

                case UserEditMode.Edit:
                    WindowTitle = $"Edit User: {editingUser?.Username}";
                    IsUsernameEditable = false; // username cannot be changed
                    Username = editingUser?.Username ?? "";
                    PasswordLabel = "New Password (leave blank to keep current)";
                    ConfirmPasswordLabel = "Confirm New Password";
                    ShowConfirmPassword = true;
                    ShowAdminFields = true;
                    SelectedRole = editingUser?.Role ?? UserRole.User;
                    IsActive = editingUser?.IsActive ?? true;
                    break;

                case UserEditMode.ChangePassword:
                    WindowTitle = "Change Password";
                    IsUsernameEditable = false;
                    Username = currentUser?.Username ?? "";
                    PasswordLabel = "Current Password";
                    ConfirmPasswordLabel = "New Password";
                    ShowConfirmPassword = true;
                    ShowAdminFields = false;
                    break;
            }

            SaveCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => Completed?.Invoke(this, false));
        }

        private async Task SaveAsync()
        {
            try
            {
                // Validate
                if (string.IsNullOrWhiteSpace(Username))
                {
                    MessageBox.Show("Username cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // For Add and ChangePassword, password must be provided
                if (_mode == UserEditMode.Add || _mode == UserEditMode.ChangePassword)
                {
                    if (string.IsNullOrWhiteSpace(Password))
                    {
                        MessageBox.Show("Password cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (Password != ConfirmPassword)
                    {
                        MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Check if username already exists (only for Add)
                if (_mode == UserEditMode.Add)
                {
                    var existing = await _unitOfWork.Users.FindAsync(u => u.Username == Username);
                    if (existing.Any())
                    {
                        MessageBox.Show("Username already exists. Please choose a different username.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (_mode == UserEditMode.Add)
                {
                    var newUser = new User
                    {
                        Username = Username,
                        PasswordHash = PasswordHelper.HashPassword(Password),
                        Role = SelectedRole,
                        IsActive = IsActive
                    };
                    await _unitOfWork.Users.AddAsync(newUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"Admin added new user: {Username}");
                }
                else if (_mode == UserEditMode.Edit)
                {
                    // Update role and active status
                    _editingUser.Role = SelectedRole;
                    _editingUser.IsActive = IsActive;
                    // Only update password if a new one was provided
                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        if (Password != ConfirmPassword)
                        {
                            MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        _editingUser.PasswordHash = PasswordHelper.HashPassword(Password);
                        _logger.LogInfo($"Admin updated password for user: {_editingUser.Username}");
                    }
                    await _unitOfWork.Users.UpdateAsync(_editingUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"Admin updated user: {_editingUser.Username}");
                }
                else // ChangePassword
                {
                    // Verify current password
                    if (!PasswordHelper.VerifyPassword(Password, _currentUser.PasswordHash))
                    {
                        MessageBox.Show("Current password is incorrect.", "Invalid Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // Set new password
                    _currentUser.PasswordHash = PasswordHelper.HashPassword(ConfirmPassword);
                    await _unitOfWork.Users.UpdateAsync(_currentUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"User {_currentUser.Username} changed their own password.");
                }

                MessageBox.Show("Operation completed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                Completed?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Save user failed: {ex.Message}", ex);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}