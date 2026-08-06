using System;
using SKAuto.Core.Services;

namespace SKAuto.Mac.Services
{
    public class MacMessageBoxService : IMessageBoxService
    {
        public MessageBoxResultType Show(string text, string caption, MessageBoxButtonType button, MessageBoxImageType icon)
        {
            // TODO: Replace with native macOS alert (e.g., using MAUI Alert)
            throw new NotImplementedException("MessageBox not implemented for Mac yet.");
        }
    }
}