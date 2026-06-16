using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace SKAuto.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigurationService _configService;
        private AppConfig _originalConfig;

        [ObservableProperty]
        private string _language;

        // Color strings (hex)
        [ObservableProperty]
        private string _headerBackground;
        [ObservableProperty]
        private string _headerForeground;
        [ObservableProperty]
        private string _alternateRowBackground;
        [ObservableProperty]
        private string _borderColor;
        [ObservableProperty]
        private string _totalBackground;
        [ObservableProperty]
        private string _accentColor;

        // Font properties
        [ObservableProperty]
        private string _fontFamily;
        [ObservableProperty]
        private int _headerFontSize;
        [ObservableProperty]
        private int _normalFontSize;
        [ObservableProperty]
        private int _titleFontSize;
        [ObservableProperty]
        private int _maxBackupsToKeep = 30;

        // Available font families (for combo box)
        public List<string> FontFamilies { get; } = new()
        {
            "Helvetica",
            "Times",
            "Courier"
        };

        public IRelayCommand SaveCommand { get; }
        public IRelayCommand CancelCommand { get; }
        public IRelayCommand PickColorCommand { get; }

        public SettingsViewModel(IConfigurationService configService)
        {
            _configService = configService;

            // Load current config asynchronously in constructor (fire and forget)
            _ = LoadConfigAsync();

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(CloseWindow);
            PickColorCommand = new RelayCommand<string>(PickColor);
        }

        private async Task LoadConfigAsync()
        {
            _originalConfig = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
            Language = _originalConfig.Language;
            HeaderBackground = _originalConfig.ReportColors.HeaderBackground;
            HeaderForeground = _originalConfig.ReportColors.HeaderForeground;
            AlternateRowBackground = _originalConfig.ReportColors.AlternateRowBackground;
            BorderColor = _originalConfig.ReportColors.Border;
            TotalBackground = _originalConfig.ReportColors.TotalBackground;
            AccentColor = _originalConfig.ReportColors.Accent;
            FontFamily = _originalConfig.ReportFonts.FontFamily;
            HeaderFontSize = _originalConfig.ReportFonts.HeaderSize;
            NormalFontSize = _originalConfig.ReportFonts.NormalSize;
            TitleFontSize = _originalConfig.ReportFonts.TitleSize;
        }

        private async void Save()
        {
            var newConfig = new AppConfig
            {
                Language = Language,
                ReportColors = new ReportColors
                {
                    HeaderBackground = HeaderBackground,
                    HeaderForeground = HeaderForeground,
                    AlternateRowBackground = AlternateRowBackground,
                    Border = BorderColor,
                    TotalBackground = TotalBackground,
                    Accent = AccentColor
                },
                ReportFonts = new ReportFonts
                {
                    FontFamily = FontFamily,
                    HeaderSize = HeaderFontSize,
                    NormalSize = NormalFontSize,
                    TitleSize = TitleFontSize
                }
            };

            await _configService.SetAsync("AppConfig", newConfig);

            // Apply culture immediately (optional)
            try
            {
                var culture = new CultureInfo(newConfig.Language);
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch { }

            MessageBox.Show(
                "Settings saved. Language changes require restart to take full effect.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            CloseWindow();
        }

        private void PickColor(string propertyName)
        {
            // Use a standard color picker dialog (you can use ColorDialog from Microsoft.Win32 or a custom one)
            // For brevity, we'll simulate with a hex input dialog. In a real implementation, use a ColorPicker.
            // We'll assume you have a method to pick a color and return hex string.
            // Example:
            // var colorDialog = new ColorDialog();
            // if (colorDialog.ShowDialog() == true)
            // {
            //     var hex = $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
            //     switch (propertyName)
            //     {
            //         case nameof(HeaderBackground): HeaderBackground = hex; break;
            //         // ...
            //     }
            // }
        }

        private void CloseWindow()
        {
            foreach (Window window in Application.Current.Windows)
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
        }
    }
}