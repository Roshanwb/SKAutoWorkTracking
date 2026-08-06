namespace SKAuto.Core.Services
{
    public interface IMessageBoxService
    {
        MessageBoxResultType Show(string messageBoxText, string caption, MessageBoxButtonType button, MessageBoxImageType icon);
    }
}