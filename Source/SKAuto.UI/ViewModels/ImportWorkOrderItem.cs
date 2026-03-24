using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using SKAuto.Core.DTOs;
using System;

namespace SKAuto.UI.ViewModels
{
    public class ImportWorkOrderItem : ObservableObject
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ImportWorkOrderDto Data { get; }

        public ImportWorkOrderItem(ImportWorkOrderDto dto)
        {
            Data = dto;
        }

        // Expose DTO properties for binding
        public string Chassis => Data.Chassis;
        public string Model => Data.Model;
        public string ClientName => Data.ClientName;
        public DateTime OrderDate => Data.OrderDate;
        public string Source => Data.Source;

        public event EventHandler? SelectionChanged;
    }
}
