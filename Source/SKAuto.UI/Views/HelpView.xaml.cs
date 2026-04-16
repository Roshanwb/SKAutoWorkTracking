using System.Windows;

namespace SKAuto.UI.Views
{
    public partial class HelpView : Window
    {
        public HelpView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}