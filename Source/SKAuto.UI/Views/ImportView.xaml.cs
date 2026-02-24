using System.Windows;
using System.Windows.Controls;

namespace SKAuto.UI.Views
{
    public partial class ImportView : Window
    {
        public ImportView()
        {
            InitializeComponent();
        }

        private void MarkSelected_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ViewModels.ImportViewModel;
            if (viewModel == null) return;

            // Get the selected items from the DataGrid
            var selectedItems = PreviewGrid.SelectedItems;
            if (selectedItems != null)
            {
                viewModel.MarkSelected((IList<object>)selectedItems);
            }
        }
    }
}