using System;
using SKAuto.Core.Services;

namespace SKAuto.Mac.Services
{
    public class MacDispatcherService : IDispatcherService
    {
        public void BeginInvoke(Action action)
        {
            // For now, just invoke directly. Later, we can use OpenSilver's dispatcher.
            action();
        }
    }
}