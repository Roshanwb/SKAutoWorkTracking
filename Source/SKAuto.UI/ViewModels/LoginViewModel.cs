using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Helpers;
using SKAuto.Core.Interfaces;

namespace SKAuto.UI.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoggingService _logger;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public IAsyncRelayCommand LoginCommand { get; }

        public event EventHandler<bool> LoginCompleted;

        public LoginViewModel(IUnitOfWork unitOfWork, ILoggingService logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            LoginCommand = new AsyncRelayCommand(LoginAsync);
        }

        private async Task LoginAsync()
        {
            ErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter username and password.";
                return;
            }

            try
            {
                var user = (await _unitOfWork.Users.FindAsync(u => u.Username == Username)).FirstOrDefault();
                if (user == null || !user.IsActive)
                {
                    ErrorMessage = "Invalid username or password.";
                    _logger.LogWarning($"Login failed: User '{Username}' not found or inactive.");
                    return;
                }

                // Verify password
                if (!PasswordHelper.VerifyPassword(Password, user.PasswordHash))
                {
                    ErrorMessage = "Invalid username or password.";
                    _logger.LogWarning($"Login failed: Invalid password for user '{Username}'.");
                    return;
                }

                // Set current user and close login window
                App.CurrentUser = user;
                _logger.LogInfo($"User '{Username}' logged in successfully with role {user.Role}.");
                LoginCompleted?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error: {ex.Message}", ex);
                ErrorMessage = "An error occurred. Please try again.";
            }
        }

        private string _password;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }
    }
}