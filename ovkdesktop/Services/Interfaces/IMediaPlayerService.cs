using System;
using System.Threading.Tasks;

namespace ovkdesktop.Services.Interfaces
{
    public interface IMediaPlayerService : IDisposable
    {
        bool IsPlaying { get; }
        TimeSpan Position { get; set; }
        TimeSpan Duration { get; }
        double Volume { get; set; }

        event EventHandler MediaOpened;
        event EventHandler MediaEnded;
        event EventHandler<Exception> MediaFailed;
        event EventHandler<TimeSpan> PositionChanged;
        event EventHandler<bool> PlaybackStateChanged;

        void Play();
        void Pause();
        Task SetSourceAsync(Uri uri);
    }
}
