using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.Entities;
using SKAuto.Core.Enums;
using SKAuto.Core.Interfaces;
using SKAuto.Import.Parsers;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;


namespace SKAuto.UI.ViewModels
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IUnitOfWork _unitOfWork;
        public ObservableCollection<PdfWorkOrder> PreviewOrders { get; set; }
        public ICommand SelectPdfCommand { get; }
        public ICommand ImportCommand { get; }
        public ImportViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            SelectPdfCommand = new RelayCommand(async () => await SelectPdfAsync());
            ImportCommand = new RelayCommand(async () => await ImportAsync());
        }

        private async Task SelectPdfAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PDF files|*.pdf" };
            if (dialog.ShowDialog() == true)
            {
                var parser = new SimplePdfParser();
                var orders = parser.Parse(dialog.FileName);
                PreviewOrders = new ObservableCollection<PdfWorkOrder>(orders);
                OnPropertyChanged(nameof(PreviewOrders));
            }
        }

        private async Task ImportAsync()
        {
            foreach (var item in PreviewOrders)
            {
                // Ensure vehicle exists
                var vehicle = (await _unitOfWork.Vehicles.FindAsync(v => v.ChassisNumber == item.Chassis)).FirstOrDefault();
                if (vehicle == null)
                {
                    vehicle = new Vehicle { ChassisNumber = item.Chassis, Model = item.Model };
                    await _unitOfWork.Vehicles.AddAsync(vehicle);
                }

                // Ensure client exists (simple name lookup)
                var client = (await _unitOfWork.Clients.FindAsync(c => c.Name == item.ClientName)).FirstOrDefault();
                if (client == null)
                {
                    client = new Client { Name = item.ClientName, Type = ClientType.Direct };
                    await _unitOfWork.Clients.AddAsync(client);
                }

                // Create work order (no tasks yet)
                var workOrder = new WorkOrder
                {
                    ClientId = client.Id,
                    VehicleId = vehicle.Id,
                    OrderDate = item.OrderDate,
                    Status = WorkStatus.Planned,
                    OrderType = OrderType.Direct_Fitting // default
                };
                await _unitOfWork.WorkOrders.AddAsync(workOrder);
            }
            await _unitOfWork.CompleteAsync();
            // Close window after success
            CloseWindow();
        }

        private void CloseWindow()
        {
            foreach (Window w in Application.Current.Windows)
                if (w.DataContext == this) { w.DialogResult = true; w.Close(); break; }
        }
    }
}