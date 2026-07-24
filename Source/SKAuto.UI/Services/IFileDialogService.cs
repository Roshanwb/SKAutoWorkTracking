namespace SKAuto.Core.Interfaces
{
    public interface IFileDialogService
    {
        /// <summary>
        /// Opens a dialog to select a file. Returns the file path or null if canceled.
        /// </summary>
        string? OpenFile(string filter = "All files (*.*)|*.*");

        /// <summary>
        /// Opens a dialog to save a file. Returns the file path or null if canceled.
        /// </summary>
        string? SaveFile(string defaultExt = ".pdf", string filter = "PDF Documents (*.pdf)|*.pdf");
    }
}
