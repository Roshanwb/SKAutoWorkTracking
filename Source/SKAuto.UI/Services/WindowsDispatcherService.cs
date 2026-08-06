using SKAuto.Core.Services;

namespace SKAuto.UI.Services
{
    public class WindowsDispatcherService : IDispatcherService
    {
        public void BeginInvoke(Action action)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
        }
    }
}