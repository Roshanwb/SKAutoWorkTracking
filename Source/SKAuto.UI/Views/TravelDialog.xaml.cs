using System.Windows;

namespace SKAuto.UI.Views
{
    public partial class TravelDialog : Window
    {
        public TravelDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public System.DateTime TravelDate { get; set; } = System.DateTime.Today;
        public string Destination { get; set; } = "";
        public string DistanceKm { get; set; } = "";
        public string TravelCost { get; set; } = "";
        public string Notes { get; set; } = "";

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