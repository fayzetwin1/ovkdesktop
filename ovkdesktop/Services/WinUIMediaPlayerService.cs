using System;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Media.Core;
using Windows.Media.Playback;
using System.Threading.Tasks;
using ovkdesktop.Services.Interfaces;

namespace ovkdesktop.Services
{
    public class WinUIMediaPlayerService : IMediaPlayerService
    {
        private readonly MediaPlayer _mediaPlayer;
        private readonly DispatcherTimer _positionTimer;
        private bool _isPlaying;

        public event EventHandler MediaOpened;
        public event EventHandler MediaEnded;
        public event EventHandler<Exception> MediaFailed;
        public event EventHandler<TimeSpan> PositionChanged;
        public event EventHandler<bool> PlaybackStateChanged;

        public WinUIMediaPlayerService()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
            _mediaPlayer.CommandManager.IsEnabled = true;
            _mediaPlayer.Volume = 1.0;

            _mediaPlayer.MediaOpened += (s, e) => MediaOpened?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.MediaEnded += (s, e) => MediaEnded?.Invoke(this, EventArgs.Empty);
            _mediaPlayer.MediaFailed += (s, e) => MediaFailed?.Invoke(this, new Exception(e.ErrorMessage));
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            _positionTimer = new DispatcherTimer();
            _positionTimer.Interval = TimeSpan.FromMilliseconds(200);
            _positionTimer.Tick += PositionTimer_Tick;
        }

        public bool IsPlaying => _isPlaying;

        public TimeSpan Position
        {
            get => _mediaPlayer.PlaybackSession?.Position ?? TimeSpan.Zero;
            set
            {
                if (_mediaPlayer.PlaybackSession != null && _mediaPlayer.PlaybackSession.CanSeek)
                {
                    _mediaPlayer.PlaybackSession.Position = value;
                    PositionChanged?.Invoke(this, value);
                }
            }
        }

        public TimeSpan Duration
        {
            get
            {
                if (_mediaPlayer.PlaybackSession == null) return TimeSpan.Zero;
                var natural = _mediaPlayer.PlaybackSession.NaturalDuration;
                if (natural.TotalSeconds > 0) return natural;
                if (_mediaPlayer.Source is MediaSource ms && ms.Duration.HasValue && ms.Duration.Value.TotalSeconds > 0)
                    return ms.Duration.Value;
                return TimeSpan.Zero;
            }
        }

        public double Volume
        {
            get => _mediaPlayer.Volume * 100.0;
            set => _mediaPlayer.Volume = Math.Clamp(value / 100.0, 0.0, 1.0);
        }

        public void Play()
        {
            _mediaPlayer.Play();
            _isPlaying = true;
            PlaybackStateChanged?.Invoke(this, _isPlaying);
            _positionTimer.Start();
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
            PlaybackStateChanged?.Invoke(this, _isPlaying);
            _positionTimer.Stop();
        }

        public async Task SetSourceAsync(Uri uri)
        {
            try
            {
                if (uri.IsFile)
                {
                    var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(uri.LocalPath);
                    _mediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
                }
                else
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(uri);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WinUIMediaPlayerService] Error setting source: {ex}");
                MediaFailed?.Invoke(this, ex);
            }
        }

        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState == MediaPlaybackState.Playing)
            {
                _isPlaying = true;
                _positionTimer.Start();
            }
            else
            {
                _isPlaying = false;
                _positionTimer.Stop();
            }

            // Must use a dispatcher if we are going to update the UI
            App.MainWindow?.DispatcherQueue?.TryEnqueue(() =>
            {
                PlaybackStateChanged?.Invoke(this, _isPlaying);
            });
        }

        private void PositionTimer_Tick(object sender, object e)
        {
            if (_isPlaying && _mediaPlayer.PlaybackSession != null)
            {
                PositionChanged?.Invoke(this, _mediaPlayer.PlaybackSession.Position);
            }
        }

        public void Dispose()
        {
            _positionTimer?.Stop();
            _mediaPlayer?.Dispose();
        }
    }
}
