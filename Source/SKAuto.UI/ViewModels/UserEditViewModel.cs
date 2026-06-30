using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Helpers;
using SKAuto.Core.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SKAuto.UI.ViewModels
{
    public enum UserEditMode
    {
        Add,
        Edit,
        ChangePassword,
        ResetPassword
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
        private string _email;

        [ObservableProperty]
        private bool _showEmailField;

        [ObservableProperty]
        private bool _isEmailEditable = true;

        [ObservableProperty]
        private string _passwordLabel = "Password";

        [ObservableProperty]
        private string _confirmPasswordLabel = "Confirm Password";

        // NEW: For ChangePassword mode, we'll have a separate NewPassword field
        [ObservableProperty]
        private string _newPasswordLabel = "New Password";

        [ObservableProperty]
        private bool _showConfirmPassword = true;

        [ObservableProperty]
        private bool _showNewPasswordField; // For ChangePassword mode (to show second password field)

        [ObservableProperty]
        private bool _showAdminFields;

        [ObservableProperty]
        private UserRole _selectedRole;

        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string _verificationCode;

        [ObservableProperty]
        private bool _showVerificationCodeField;

        [ObservableProperty]
        private string _actionButtonText = "Save";

        public string Password { get; set; }       // Current password (for ChangePassword) or regular password
        public string NewPassword { get; set; }    // New password (only for ChangePassword)
        public string ConfirmPassword { get; set; }

        public Array RoleValues => Enum.GetValues(typeof(UserRole));

        public IAsyncRelayCommand ActionCommand { get; }
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
                    Email = "";
                    ShowEmailField = true;
                    IsEmailEditable = true;
                    PasswordLabel = "Password";
                    ConfirmPasswordLabel = "Confirm Password";
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = false;
                    ShowAdminFields = true;
                    IsActive = true;
                    break;

                case UserEditMode.Edit:
                    WindowTitle = $"Edit User: {editingUser?.Username}";
                    IsUsernameEditable = false;
                    Username = editingUser?.Username ?? "";
                    Email = editingUser?.Email ?? "";
                    ShowEmailField = true;
                    IsEmailEditable = true;
                    PasswordLabel = "New Password (leave blank to keep current)";
                    ConfirmPasswordLabel = "Confirm New Password";
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = false;
                    ShowAdminFields = true;
                    SelectedRole = editingUser?.Role ?? UserRole.User;
                    IsActive = editingUser?.IsActive ?? true;
                    break;

                case UserEditMode.ChangePassword:
                    WindowTitle = "Change Password";
                    IsUsernameEditable = false;
                    Username = currentUser?.Username ?? "";
                    Email = currentUser?.Email ?? "";
                    ShowEmailField = false;
                    IsEmailEditable = false;
                    PasswordLabel = "Current Password";
                    NewPasswordLabel = "New Password";
                    ConfirmPasswordLabel = "Confirm New Password";
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = true;   // we will show three fields
                    ShowAdminFields = false;
                    break;

                case UserEditMode.ResetPassword:
                    WindowTitle = "Reset Password";
                    IsUsernameEditable = true;
                    Username = "";
                    Email = "";
                    ShowEmailField = false;
                    IsEmailEditable = false;
                    PasswordLabel = "New Password";
                    ConfirmPasswordLabel = "Confirm Password";
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = false;
                    ShowAdminFields = false;
                    ShowVerificationCodeField = true;
                    ActionButtonText = "Send Code";
                    break;
            }

            ActionCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => Completed?.Invoke(this, false));
        }

        private async Task SendResetCodeAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("Please enter your username.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = (await _unitOfWork.Users.FindAsync(u => u.Username == Username)).FirstOrDefault();
            if (user == null || !user.IsActive)
            {
                MessageBox.Show("User not found or inactive.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                MessageBox.Show("No email address configured for this user. Please contact an administrator.", "Email Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var code = new Random().Next(100000, 999999).ToString();
            user.ResetToken = code;
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CompleteAsync();

            try
            {
                var emailService = App.GetService<IEmailService>();
                var body = $@"
                    <h2>Password Reset Request</h2>
                    <p>You requested to reset your password for SKAuto Work Tracking.</p>
                    <p>Your verification code is: <strong>{code}</strong></p>
                    <p>This code will expire in 15 minutes.</p>
                    <p>If you did not request this, please ignore this email.</p>
                ";
                await emailService.SendEmailAsync(user.Email, "SKAuto - Password Reset Code", body);
                MessageBox.Show($"A verification code has been sent to {user.Email}. Please check your inbox.", "Code Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Net.Mail.SmtpException smtpEx)
            {
                _logger.LogError($"SMTP error sending email to {user.Email}", smtpEx);
                MessageBox.Show($"Email could not be sent.", "Email Failed ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send reset email to {user.Email}", ex);
                MessageBox.Show($"Email could not be sent.", "Email Failed ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ActionButtonText = "Verify & Reset";
        }

        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    MessageBox.Show("Username cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // --- RESET PASSWORD FLOW ---
                if (_mode == UserEditMode.ResetPassword)
                {
                    if (ActionButtonText == "Send Code")
                    {
                        await SendResetCodeAsync();
                        return;
                    }

                    var user = (await _unitOfWork.Users.FindAsync(u => u.Username == Username)).FirstOrDefault();
                    if (user == null || !user.IsActive)
                    {
                        MessageBox.Show("User not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (user.ResetToken != VerificationCode || user.ResetTokenExpiry < DateTime.UtcNow)
                    {
                        MessageBox.Show("Invalid or expired verification code.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Password) || Password != ConfirmPassword)
                    {
                        MessageBox.Show("Passwords do not match or are empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (Password.Length < 6)
                    {
                        MessageBox.Show("Password must be at least 6 characters.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    user.PasswordHash = PasswordHelper.HashPassword(Password);
                    user.ResetToken = null;
                    user.ResetTokenExpiry = null;
                    await _unitOfWork.Users.UpdateAsync(user);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"Password reset for user {Username}");
                    MessageBox.Show("Password reset successfully. Please log in.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Completed?.Invoke(this, true);
                    return;
                }

                // --- ADD / EDIT VALIDATION ---
                if (_mode == UserEditMode.Add || _mode == UserEditMode.Edit)
                {
                    // Validate email
                    if (string.IsNullOrWhiteSpace(Email))
                    {
                        MessageBox.Show("Email is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    try
                    {
                        var addr = new System.Net.Mail.MailAddress(Email);
                        if (addr.Address != Email)
                            throw new FormatException();
                    }
                    catch
                    {
                        MessageBox.Show("Invalid email format.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // For Add, password is required
                    if (_mode == UserEditMode.Add)
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

                    // For Edit, password is optional (only if user wants to change it)
                    if (_mode == UserEditMode.Edit)
                    {
                        if (!string.IsNullOrWhiteSpace(Password) && Password != ConfirmPassword)
                        {
                            MessageBox.Show("Passwords do not match.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                }

                // --- CHANGE PASSWORD FLOW ---
                if (_mode == UserEditMode.ChangePassword)
                {
                    // Verify current password
                    if (!PasswordHelper.VerifyPassword(Password, _currentUser.PasswordHash))
                    {
                        MessageBox.Show("Current password is incorrect.", "Invalid Password", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Validate new password
                    if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
                    {
                        MessageBox.Show("New passwords do not match or are empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (NewPassword.Length < 6)
                    {
                        MessageBox.Show("New password must be at least 6 characters.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Set new password
                    _currentUser.PasswordHash = PasswordHelper.HashPassword(NewPassword);
                    await _unitOfWork.Users.UpdateAsync(_currentUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"User {_currentUser.Username} changed their own password.");
                    MessageBox.Show("Password changed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    Completed?.Invoke(this, true);
                    return;
                }

                // --- ADD NEW USER ---
                if (_mode == UserEditMode.Add)
                {
                    var existing = await _unitOfWork.Users.FindAsync(u => u.Username == Username);
                    if (existing.Any())
                    {
                        MessageBox.Show("Username already exists.", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newUser = new User
                    {
                        Username = Username,
                        PasswordHash = PasswordHelper.HashPassword(Password),
                        Email = Email,
                        Role = SelectedRole,
                        IsActive = IsActive
                    };
                    await _unitOfWork.Users.AddAsync(newUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"Admin added new user: {Username} with email {Email}");
                }

                // --- EDIT USER ---
                else if (_mode == UserEditMode.Edit)
                {
                    _editingUser.Role = SelectedRole;
                    _editingUser.IsActive = IsActive;
                    _editingUser.Email = Email;
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
                    _logger.LogInfo($"Admin updated user: {_editingUser.Username} (email: {Email})");
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