using SKAuto.UI.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace SKAuto.UI.Views
{
    public partial class LoginView : Window
    {
        private readonly LoginViewModel _viewModel;

        public LoginView(LoginViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            _viewModel.LoginCompleted += OnLoginCompleted;

            // Focus the username field initially
            Loaded += (s, e) => UsernameBox.Focus();

            // Hook key down events
            UsernameBox.KeyDown += UsernameBox_KeyDown;
            PasswordBox.KeyDown += PasswordBox_KeyDown;
            PreviewKeyDown += LoginView_PreviewKeyDown; // Escape key
        }

        private void OnLoginCompleted(object sender, bool success)
        {
            DialogResult = success;
            Close();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        }

        // Enter on Username: move focus to Password
        private void UsernameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PasswordBox.Focus();
                e.Handled = true;
            }
        }

        // Enter on Password: trigger login
        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.LoginCommand.CanExecute(null))
            {
                _viewModel.LoginCommand.Execute(null);
                e.Handled = true;
            }
        }

        // Escape anywhere: exit the application
        private void LoginView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Application.Current.Shutdown();
                e.Handled = true;
            }
        }
    }
}