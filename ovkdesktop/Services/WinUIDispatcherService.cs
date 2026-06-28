using System;
using ovkdesktop.Services.Interfaces;

namespace ovkdesktop.Services
{
    public class WinUIDispatcherService : IDispatcherService
    {
        public void TryEnqueue(Action action)
        {
            if (App.MainWindow?.DispatcherQueue != null)
            {
                App.MainWindow.DispatcherQueue.TryEnqueue(() => action());
            }
            else
            {
                // Fallback or execute directly if not available (for tests etc)
                action();
            }
        }
    }
}
