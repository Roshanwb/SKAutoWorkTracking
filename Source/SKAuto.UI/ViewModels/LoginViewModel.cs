using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SKAuto.UI.ViewModels
{
    public class LoginViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        public string Username { get; set; }
        public string Password { get; set; }
        public ICommand LoginCommand { get; }
        public event EventHandler<bool> LoginCompleted;

        public LoginViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            LoginCommand = new AsyncRelayCommand(LoginAsync);
        }

        private async Task LoginAsync()
        {
            var user = (await _unitOfWork.Users.FindAsync(u => u.Username == Username && u.PasswordHash == Password)).FirstOrDefault();
            LoginCompleted?.Invoke(this, user != null && user.Role == "Admin");
        }
    }
}
