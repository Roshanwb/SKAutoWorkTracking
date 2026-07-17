using System.Windows;
using SKAuto.Core.Services;

namespace SKAuto.UI.Services
{
    public class WindowsMessageBoxService : IMessageBoxService
    {
        public MessageBoxResultType Show(string text, string caption, MessageBoxButtonType button, MessageBoxImageType icon)
        {
            // Convert our enums to WPF equivalents
            var wpfButton = button switch
            {
                MessageBoxButtonType.OK => MessageBoxButton.OK,
                MessageBoxButtonType.OKCancel => MessageBoxButton.OKCancel,
                MessageBoxButtonType.YesNo => MessageBoxButton.YesNo,
                MessageBoxButtonType.YesNoCancel => MessageBoxButton.YesNoCancel,
                _ => MessageBoxButton.OK
            };

            var wpfIcon = icon switch
            {
                MessageBoxImageType.Information => MessageBoxImage.Information,
                MessageBoxImageType.Warning => MessageBoxImage.Warning,
                MessageBoxImageType.Error => MessageBoxImage.Error,
                MessageBoxImageType.Question => MessageBoxImage.Question,
                _ => MessageBoxImage.None
            };

            var result = System.Windows.MessageBox.Show(text, caption, wpfButton, wpfIcon);

            return result switch
            {
                MessageBoxResult.OK => MessageBoxResultType.OK,
                MessageBoxResult.Cancel => MessageBoxResultType.Cancel,
                MessageBoxResult.Yes => MessageBoxResultType.Yes,
                MessageBoxResult.No => MessageBoxResultType.No,
                _ => MessageBoxResultType.None
            };
        }
    }
}