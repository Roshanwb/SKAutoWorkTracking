using System.Windows;

namespace SKAuto.UI.Views
{
    public partial class VehicleWorkOrdersView : Window
    {
        public VehicleWorkOrdersView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}