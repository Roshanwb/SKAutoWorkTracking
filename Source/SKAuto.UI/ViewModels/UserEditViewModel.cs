using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Helpers;
using SKAuto.Core.Interfaces;

using SKAuto.UI.Localization;
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
        private string _passwordLabel = LocalizationManager.Instance["LoginView_Password"];

        [ObservableProperty]
        private string _confirmPasswordLabel = LocalizationManager.Instance["ConfirmPassword"];

        // NEW: For ChangePassword mode, we'll have a separate NewPassword field
        [ObservableProperty]
        private string _newPasswordLabel = LocalizationManager.Instance["NewPassword"];

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
        private string _actionButtonText = LocalizationManager.Instance["VehicleEditWindow_Save"];

        public string Password { get; set; }       // Current password (for ChangePassword) or regular password
        public string NewPassword { get; set; }    // New password (only for ChangePassword)
        public string ConfirmPassword { get; set; }

        public Array RoleValues => Enum.GetValues(typeof(UserRole));

        public IAsyncRelayCommand ActionCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public event EventHandler<bool> Completed;

        private bool _isSendCode = false;

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
                    WindowTitle = LocalizationManager.Instance["AddNewUser"];
                    IsUsernameEditable = true;
                    Username = "";
                    Email = "";
                    ShowEmailField = true;
                    IsEmailEditable = true;
                    PasswordLabel = LocalizationManager.Instance["LoginView_Password"];
                    ConfirmPasswordLabel = LocalizationManager.Instance["ConfirmPassword"];
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = false;
                    ShowAdminFields = true;
                    IsActive = true;
                    _isSendCode = false;
                    break;

                case UserEditMode.Edit:
                    WindowTitle = $"{LocalizationManager.Instance["MainWindow_Edit"]}: {editingUser?.Username}";
                    IsUsernameEditable = false;
                    Username = editingUser?.Username ?? "";
                    Email = editingUser?.Email ?? "";
                    ShowEmailField = true;
                    IsEmailEditable = true;
                    PasswordLabel = LocalizationManager.Instance["NewPassword"];
                    ConfirmPasswordLabel = LocalizationManager.Instance["ConfirmNewPassword"];
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = false;
                    ShowAdminFields = true;
                    SelectedRole = editingUser?.Role ?? UserRole.User;
                    IsActive = editingUser?.IsActive ?? true;
                    _isSendCode = false;
                    break;

                case UserEditMode.ChangePassword:
                    WindowTitle = LocalizationManager.Instance["MainWindow_ChangePassword"];
                    IsUsernameEditable = false;
                    Username = currentUser?.Username ?? "";
                    Email = currentUser?.Email ?? "";
                    ShowEmailField = false;
                    IsEmailEditable = false;
                    PasswordLabel = LocalizationManager.Instance["CurrentPassword"];
                    NewPasswordLabel = LocalizationManager.Instance["NewPassword"];
                    ConfirmPasswordLabel = LocalizationManager.Instance["ConfirmNewPassword"];
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = true;   // we will show three fields
                    ShowAdminFields = false;
                    _isSendCode = false;
                    break;

                case UserEditMode.ResetPassword:
                    WindowTitle = LocalizationManager.Instance["ResetPassword"];
                    IsUsernameEditable = true;
                    Username = "";
                    Email = "";
                    ShowEmailField = false;
                    IsEmailEditable = false;
                    PasswordLabel = LocalizationManager.Instance["NewPassword"];
                    ConfirmPasswordLabel = LocalizationManager.Instance["ConfirmNewPassword"];
                    ShowConfirmPassword = true;
                    ShowNewPasswordField = false;
                    ShowAdminFields = false;
                    ShowVerificationCodeField = true;
                    ActionButtonText = LocalizationManager.Instance["SendCode"];
                    _isSendCode = true;
                    break;
            }

            ActionCommand = new AsyncRelayCommand(SaveAsync);
            CancelCommand = new RelayCommand(() => Completed?.Invoke(this, false));
        }

        private async Task SendResetCodeAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                System.Windows.MessageBox.Show(LocalizationManager.Instance["PleaseEnterUsername"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var user = (await _unitOfWork.Users.FindAsync(u => u.Username == Username)).FirstOrDefault();
            if (user == null || !user.IsActive)
            {
                System.Windows.MessageBox.Show(LocalizationManager.Instance["UserNotFoundOrInactive"], "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                System.Windows.MessageBox.Show(LocalizationManager.Instance["NoEmailAddressConfigured"], "Email Missing", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                var body = $@"{LocalizationManager.Instance["CodeEmailBodyPart1"]} {code} {LocalizationManager.Instance["CodeEmailBodyPart2"]} ";
                await emailService.SendEmailAsync(user.Email, LocalizationManager.Instance["CodeEmailTitle"], body);
                System.Windows.MessageBox.Show($"{LocalizationManager.Instance["CodeSentMessageBodySuccessPart1"]} {user.Email} . {LocalizationManager.Instance["CodeSentMessageBodySuccessPart2"]}", LocalizationManager.Instance["CodeSent"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (System.Net.Mail.SmtpException smtpEx)
            {
                _logger.LogError($"SMTP error sending email to {user.Email}", smtpEx);
                System.Windows.MessageBox.Show(LocalizationManager.Instance["CodeSentMessageBodyFailed"], LocalizationManager.Instance["EmailFailed"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send reset email to {user.Email}", ex);
                System.Windows.MessageBox.Show(LocalizationManager.Instance["CodeSentMessageBodyFailed"], LocalizationManager.Instance["EmailFailed"], System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

            ActionButtonText = LocalizationManager.Instance["VerifyReset"];
        }

        private async Task SaveAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Username))
                {
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["UsernameCannotBeEmpty"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // --- RESET PASSWORD FLOW ---
                if (_mode == UserEditMode.ResetPassword)
                {
                    if (_isSendCode)
                    {
                        await SendResetCodeAsync();
                        return;
                    }

                    var user = (await _unitOfWork.Users.FindAsync(u => u.Username == Username)).FirstOrDefault();
                    if (user == null || !user.IsActive)
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["UserNotFound"], "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    if (user.ResetToken != VerificationCode || user.ResetTokenExpiry < DateTime.UtcNow)
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["InvalidOrExpiredVerificationCode"], "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Password) || Password != ConfirmPassword)
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordsDoNotMatchOrEmpty"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    if (Password.Length < 6)
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordMinimumLength"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    user.PasswordHash = PasswordHelper.HashPassword(Password);
                    user.ResetToken = null;
                    user.ResetTokenExpiry = null;
                    await _unitOfWork.Users.UpdateAsync(user);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"Password reset for user {Username}");
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordResetSuccessfully"], "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    Completed?.Invoke(this, true);
                    return;
                }

                // --- ADD / EDIT VALIDATION ---
                if (_mode == UserEditMode.Add || _mode == UserEditMode.Edit)
                {
                    // Validate email
                    if (string.IsNullOrWhiteSpace(Email))
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["EmailRequired"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["InvalidEmailFormat"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // For Add, password is required
                    if (_mode == UserEditMode.Add)
                    {
                        if (string.IsNullOrWhiteSpace(Password))
                        {
                            System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordCannotBeEmpty"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            return;
                        }
                        if (Password != ConfirmPassword)
                        {
                            System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordsDoNotMatch"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            return;
                        }
                    }

                    // For Edit, password is optional (only if user wants to change it)
                    if (_mode == UserEditMode.Edit)
                    {
                        if (!string.IsNullOrWhiteSpace(Password) && Password != ConfirmPassword)
                        {
                            System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordsDoNotMatch"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["CurrentPasswordIncorrect"], "Invalid Password", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // Validate new password
                    if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmPassword)
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["NewPasswordsDoNotMatchOrEmpty"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    if (NewPassword.Length < 6)
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["NewPasswordMinimumLength"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // Set new password
                    _currentUser.PasswordHash = PasswordHelper.HashPassword(NewPassword);
                    await _unitOfWork.Users.UpdateAsync(_currentUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"User {_currentUser.Username} changed their own password.");
                    System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordChangedSuccessfully"], "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    Completed?.Invoke(this, true);
                    return;
                }

                // --- ADD NEW USER ---
                if (_mode == UserEditMode.Add)
                {
                    var existing = await _unitOfWork.Users.FindAsync(u => u.Username == Username);
                    if (existing.Any())
                    {
                        System.Windows.MessageBox.Show(LocalizationManager.Instance["UsernameAlreadyExists"], "Duplicate", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                            System.Windows.MessageBox.Show(LocalizationManager.Instance["PasswordsDoNotMatch"], "Validation", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                            return;
                        }
                        _editingUser.PasswordHash = PasswordHelper.HashPassword(Password);
                        _logger.LogInfo($"Admin updated password for user: {_editingUser.Username}");
                    }
                    await _unitOfWork.Users.UpdateAsync(_editingUser);
                    await _unitOfWork.CompleteAsync();
                    _logger.LogInfo($"Admin updated user: {_editingUser.Username} (email: {Email})");
                }

                System.Windows.MessageBox.Show(LocalizationManager.Instance["OperationCompletedSuccessfully"], "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                Completed?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Save user failed: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}