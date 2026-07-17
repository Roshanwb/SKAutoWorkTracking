using System;

namespace SKAuto.Core.Services
{
    public interface IDispatcherService
    {
        void BeginInvoke(Action action);
    }
}