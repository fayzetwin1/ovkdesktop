using System;

namespace ovkdesktop.Services.Interfaces
{
    public interface IDispatcherService
    {
        void TryEnqueue(Action action);
    }
}
