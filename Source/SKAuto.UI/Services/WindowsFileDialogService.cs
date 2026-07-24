using SKAuto.Core.Interfaces;

namespace SKAuto.UI.Services
{
    public class WindowsFileDialogService : IFileDialogService
    {
        public string? OpenFile(string filter = "All files (*.*)|*.*")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                CheckFileExists = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? SaveFile(string defaultExt = ".pdf", string filter = "PDF Documents (*.pdf)|*.pdf")
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = defaultExt,
                Filter = filter,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}