using System.Windows;
using SKAuto.Core.Entities;

namespace SKAuto.UI.Views
{
    public partial class ClientMergeDialog : Window
    {
        public ClientMergeDialog(Client master)
        {
            InitializeComponent();

            // Populate controls from the master client
            txtName.Text = master.Name;
            txtPhone.Text = master.Phone;
            txtEmail.Text = master.Email;
            txtAddress.Text = master.Address;
            txtNotes.Text = master.Notes;
            chkActive.IsChecked = master.IsActive;
        }

        public string ClientName => txtName.Text;
        public string Phone => txtPhone.Text;
        public string Email => txtEmail.Text;
        public string Address => txtAddress.Text;
        public string Notes => txtNotes.Text;
        public bool IsActive => chkActive.IsChecked == true;

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}