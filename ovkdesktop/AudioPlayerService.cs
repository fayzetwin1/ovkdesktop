using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using ovkdesktop.Models; // add an explicit import of the model
using Newtonsoft.Json.Linq;

namespace ovkdesktop
{
    public class AudioPlayerService : IDisposable
    {
        // main player for audio playback
        private readonly MediaPlayer _mediaPlayer;
        
        // timer for tracking the playback position
        private readonly DispatcherTimer _positionTimer;
        
        // current index of the track in the playlist
        private int _currentIndex = -1;
        
        // flag of the playback state
        private bool _isPlaying = false;
        
        // current playlist
        public ObservableCollection<Models.Audio> Playlist { get; private set; } = new ObservableCollection<Models.Audio>();
        
        // current track
        public Models.Audio CurrentAudio { get; private set; }
        
        // events for notifying the UI
        public event EventHandler<Models.Audio> CurrentAudioChanged;
        public event EventHandler<bool> PlaybackStateChanged;
        public event EventHandler<TimeSpan> PositionChanged;
        public event EventHandler<double> VolumeChanged;
        
        // event for notifying about the change of the favorite status
        public event EventHandler<Models.Audio> FavoriteStatusChanged;
        
        // properties for accessing the state of the player
        public bool IsPlaying => _isPlaying;
        public TimeSpan CurrentPosition => _mediaPlayer?.PlaybackSession?.Position ?? TimeSpan.Zero;
        public TimeSpan TotalDuration 
        {
            get 
            {
                try 
                {
                    if (_mediaPlayer?.PlaybackSession == null)
                        return TimeSpan.Zero;

                    // try to get the duration from the playback session
                    TimeSpan naturalDuration = _mediaPlayer.PlaybackSession.NaturalDuration;
                    
                    // if the duration is incorrect, try to get it from the media source
                    if (naturalDuration.TotalSeconds <= 0 && _mediaPlayer.Source is MediaSource mediaSource && mediaSource.Duration.HasValue)
                    {
                        TimeSpan sourceDuration = mediaSource.Duration.Value;
                        
                        // use the duration from the source if it is valid
                        if (sourceDuration.TotalSeconds > 0)
                        {
                            Debug.WriteLine($"[AudioPlayerService] Using duration from MediaSource: {sourceDuration.TotalMinutes:F1} min");
                            return sourceDuration;
                        }
                    }
                    
                    // if the duration is valid, use it
                    if (naturalDuration.TotalSeconds > 0)
                    {
                        return naturalDuration;
                    }
                    
                    // check if there is information about the duration in the current track
                    if (CurrentAudio != null && CurrentAudio.Duration > 0)
                    {
                        TimeSpan metadataDuration = TimeSpan.FromSeconds(CurrentAudio.Duration);
                        Debug.WriteLine($"[AudioPlayerService] Using duration from metadata: {metadataDuration.TotalMinutes:F1} min");
                        return metadataDuration;
                    }
                    
                    // if there is no data, return Zero
                    return TimeSpan.Zero;
                }
                catch (Exception ex) 
                {
                    Debug.WriteLine($"[AudioPlayerService] Error getting duration: {ex.Message}");
                    return TimeSpan.Zero;
                }
            }
        }
        
        // constructor
        public AudioPlayerService()
        {
            try
            {
                // initialization of the player
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
                _mediaPlayer.CommandManager.IsEnabled = true;
                
                // setting the initial volume
                _mediaPlayer.Volume = 1.0;
                
                // subscription to the events of the player
                _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
                _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
                _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
                
                // initialization of the timer for tracking the position
                _positionTimer = new DispatcherTimer();
                _positionTimer.Interval = TimeSpan.FromMilliseconds(200); // reduce the interval for smoother update
                _positionTimer.Tick += PositionTimer_Tick;
                
                Debug.WriteLine("[AudioPlayerService] Successfully initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error initializing: {ex.Message}");
                throw;
            }
        }
        
        // getting the MediaPlayer for binding to the MediaPlayerElement
        public MediaPlayer GetMediaPlayer()
        {
            return _mediaPlayer;
        }
        
        // getting the current volume (0-100)
        public double GetVolume()
        {
            return _mediaPlayer?.Volume * 100.0 ?? 0;
        }
        
        // setting the playlist and starting playback from the specified index
        public void SetPlaylist(IEnumerable<Models.Audio> playlist, int startIndex = 0)
        {
            try
            {
                Debug.WriteLine($"[AudioPlayerService] SetPlaylist called with collection type: {playlist?.GetType().FullName ?? "null"}");
                
                if (playlist == null)
                {
                    Debug.WriteLine("[AudioPlayerService] Error: playlist is null");
                    return;
                }
                
                        // detailed information about the received playlist
                int itemCount = playlist.Count();
                Debug.WriteLine($"[AudioPlayerService] Playlist contains {itemCount} items");
                
                // checking the types of all elements before starting processing
                bool containsNonAudioTypes = false;
                int problematicIndex = -1;
                int index = 0;
                
                foreach (var item in playlist)
                {
                    if (item != null && !(item is Models.Audio))
                    {
                        containsNonAudioTypes = true;
                        problematicIndex = index;
                        Debug.WriteLine($"[AudioPlayerService] WARNING: Item at index {index} is not a Models.Audio, but {item.GetType().FullName}");
                    }
                    index++;
                }
                
                if (containsNonAudioTypes)
                {
                    Debug.WriteLine($"[AudioPlayerService] CRITICAL: Playlist contains non-Audio types! First problematic item at index {problematicIndex}");
                    // if necessary, we can make additional checks or conversions
                }
                
                // clearing the current playlist
                Playlist.Clear();
                
                // checking the types of each element before adding
                index = 0;
                foreach (var item in playlist)
                {
                    Debug.WriteLine($"[AudioPlayerService] Processing playlist item {index}: {item?.GetType().FullName ?? "null"}");
                    
                    if (item == null)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Warning: Item at index {index} is null. Skipping.");
                        index++;
                        continue;
                    }
                    
                    // checking that the element has the correct type
                    if (item is Models.Audio audio)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Adding track to playlist: {audio.Artist} - {audio.Title}, ID: {audio.Id}");
                        
                        try
                        {
                            // checking the presence of all necessary properties before adding
                            bool hasValidId = audio.Id != 0;
                            bool hasArtist = !string.IsNullOrEmpty(audio.Artist);
                            bool hasTitle = !string.IsNullOrEmpty(audio.Title);
                            bool hasUrl = !string.IsNullOrEmpty(audio.Url);
                            
                            Debug.WriteLine($"[AudioPlayerService] Track validity check - ID: {hasValidId}, Artist: {hasArtist}, Title: {hasTitle}, URL: {hasUrl}");
                            
                            // saving the types of properties for diagnostics
                            Debug.WriteLine($"[AudioPlayerService] Property types: Artist={audio.Artist?.GetType().FullName ?? "null"}, " +
                                            $"Title={audio.Title?.GetType().FullName ?? "null"}, Url={audio.Url?.GetType().FullName ?? "null"}");
                            
                            // adding the track to the playlist
                    Playlist.Add(audio);
                }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[AudioPlayerService] Error adding audio to playlist: {ex.GetType().FullName}: {ex.Message}");
                            Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[AudioPlayerService] ERROR: Item at index {index} is not a Models.Audio. Type: {item.GetType().FullName}");
                        // trying to convert to Models.Audio, if this is not the correct type
                        try
                        {
                            // using reflection to determine the properties of the object
                            var properties = item.GetType().GetProperties();
                            Debug.WriteLine($"[AudioPlayerService] Object properties: {string.Join(", ", properties.Select(p => p.Name))}");
                            
                            // creating a new audio object
                            Models.Audio newAudio = new Models.Audio();
                            Debug.WriteLine($"[AudioPlayerService] Created new Audio object: {newAudio.GetType().FullName}");
                            
                            // trying to set the properties
                            try { newAudio.Id = (int)item.GetType().GetProperty("Id")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting Id: {propEx.Message}"); }
                            
                            try { newAudio.OwnerId = (int)item.GetType().GetProperty("OwnerId")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting OwnerId: {propEx.Message}"); }
                            
                            try { newAudio.Artist = (string)item.GetType().GetProperty("Artist")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting Artist: {propEx.Message}"); }
                            
                            try { newAudio.Title = (string)item.GetType().GetProperty("Title")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting Title: {propEx.Message}"); }
                            
                            try { newAudio.Url = (string)item.GetType().GetProperty("Url")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting Url: {propEx.Message}"); }
                            
                            try { newAudio.Duration = (int)item.GetType().GetProperty("Duration")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting Duration: {propEx.Message}"); }
                            
                            try { newAudio.IsAdded = (bool)item.GetType().GetProperty("IsAdded")?.GetValue(item, null); } 
                            catch (Exception propEx) { Debug.WriteLine($"[AudioPlayerService] Error setting IsAdded: {propEx.Message}"); }
                            
                            // checking that we have received the minimum necessary data
                            if (newAudio.Id != 0 || !string.IsNullOrEmpty(newAudio.Url))
                            {
                                Debug.WriteLine($"[AudioPlayerService] Successfully converted item to Audio: {newAudio.Artist} - {newAudio.Title}");
                                Playlist.Add(newAudio);
                            }
                            else
                            {
                                Debug.WriteLine("[AudioPlayerService] Failed to convert item to Audio - missing essential properties");
                            }
                        }
                        catch (Exception convEx)
                        {
                            Debug.WriteLine($"[AudioPlayerService] Failed to convert item: {convEx.GetType().FullName}: {convEx.Message}");
                            Debug.WriteLine($"[AudioPlayerService] Stack trace: {convEx.StackTrace}");
                        }
                    }
                    
                    index++;
                }
                
                Debug.WriteLine($"[AudioPlayerService] Playlist set with {Playlist.Count} tracks (from original {itemCount})");
                
                // if the playlist is not empty and the index is correct, start playback
                if (Playlist.Count > 0 && startIndex >= 0 && startIndex < Playlist.Count)
                {
                    _currentIndex = startIndex;
                    
                    try
                    {
                        // safe getting the corresponding audio object from our playlist
                        var audioToPlay = Playlist[startIndex];
                        Debug.WriteLine($"[AudioPlayerService] Playing track at index {startIndex}: {audioToPlay?.Artist} - {audioToPlay?.Title}, Type: {audioToPlay?.GetType().FullName ?? "null"}");
                        
                        if (audioToPlay != null)
                        {
                            PlayAudio(audioToPlay);
                        }
                        else
                        {
                            Debug.WriteLine("[AudioPlayerService] ERROR: Audio at selected index is null!");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Error starting playback: {ex.GetType().FullName}: {ex.Message}");
                        Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine("[AudioPlayerService] Invalid start index or empty playlist");
                }
            }
            catch (InvalidCastException icEx)
            {
                Debug.WriteLine($"[AudioPlayerService] InvalidCastException in SetPlaylist: {icEx.Message}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {icEx.StackTrace}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error setting playlist: {ex.Message}");
                Debug.WriteLine($"[AudioPlayerService] Exception type: {ex.GetType().FullName}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
            }
        }
        
        // playback of the specified track
        public void PlayAudio(Models.Audio audio)
        {
            try
            {
                if (audio == null)
                {
                    Debug.WriteLine("[AudioPlayerService] ERROR: Cannot play - audio is null");
                    return;
                }
                
                Debug.WriteLine($"[AudioPlayerService] PlayAudio called with audio type: {audio.GetType().FullName}");
                Debug.WriteLine($"[AudioPlayerService] Playing audio: {audio.Artist} - {audio.Title}, ID: {audio.Id}, URL: {audio.Url?.Substring(0, Math.Min(50, audio.Url?.Length ?? 0))}...");
                
                // check the URL before playback
                if (string.IsNullOrEmpty(audio.Url))
                {
                    Debug.WriteLine("[AudioPlayerService] ERROR: Cannot play - URL is empty");
                    return;
                }
                
                try
                {
                    // trying to create a URI to check the correctness of the URL
                    Uri uri = new Uri(audio.Url);
                    Debug.WriteLine($"[AudioPlayerService] URL is valid: {uri.AbsoluteUri}");
                }
                catch (UriFormatException ex)
                {
                    Debug.WriteLine($"[AudioPlayerService] ERROR: Invalid URL format: {ex.Message}");
                    return;
                }
                
                // set the current track
                CurrentAudio = audio;
                
                // check the status of the like for the current track
                _ = CheckAudioFavoriteStatusAsync(audio);
                
                // notify the UI about the change of the track
                try
                {
                    Debug.WriteLine("[AudioPlayerService] Invoking CurrentAudioChanged event");
                    CurrentAudioChanged?.Invoke(this, audio);
                }
                catch (Exception eventEx)
                {
                    Debug.WriteLine($"[AudioPlayerService] Error in CurrentAudioChanged event: {eventEx.Message}");
                }
                
                // create a source for playback
                try
                {
                    Debug.WriteLine("[AudioPlayerService] Creating media source from URI");
                    var source = MediaSource.CreateFromUri(new Uri(audio.Url));
                    _mediaPlayer.Source = source;
                }
                catch (Exception sourceEx)
                {
                    Debug.WriteLine($"[AudioPlayerService] Error creating media source: {sourceEx.GetType().FullName}: {sourceEx.Message}");
                    Debug.WriteLine($"[AudioPlayerService] Stack trace: {sourceEx.StackTrace}");
                    return;
                }
                
                // start playback
                try
                {
                    Debug.WriteLine("[AudioPlayerService] Starting playback");
                _mediaPlayer.Play();
                    _isPlaying = true;
                    
                        // notify the UI about the change of the playback state
                    PlaybackStateChanged?.Invoke(this, _isPlaying);
                
                // start the timer for tracking the position
                _positionTimer.Start();
                }
                catch (Exception playEx)
                {
                    Debug.WriteLine($"[AudioPlayerService] Error starting playback: {playEx.GetType().FullName}: {playEx.Message}");
                    Debug.WriteLine($"[AudioPlayerService] Stack trace: {playEx.StackTrace}");
                }
            }
            catch (InvalidCastException icEx)
            {
                Debug.WriteLine($"[AudioPlayerService] InvalidCastException in PlayAudio: {icEx.Message}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {icEx.StackTrace}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error playing audio: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
            }
        }
        
        // switching between playback and pause
        public void TogglePlayPause()
        {
            try
            {
                if (_mediaPlayer == null)
                {
                    Debug.WriteLine("[AudioPlayerService] ERROR: MediaPlayer is null");
                    return;
                }
                
                if (_mediaPlayer.Source == null)
                {
                    Debug.WriteLine("[AudioPlayerService] ERROR: No media source to play/pause");
                    return;
                }
                
                Debug.WriteLine($"[AudioPlayerService] TogglePlayPause called, current state: {(_isPlaying ? "Playing" : "Paused")}");
                
                try
                {
                if (_isPlaying)
                {
                    // if playback is in progress, put it on pause
                    Debug.WriteLine("[AudioPlayerService] Pausing playback");
                    _mediaPlayer.Pause();
                        _isPlaying = false;
                }
                else
                {
                    // if paused, continue playback
                    Debug.WriteLine("[AudioPlayerService] Resuming playback");
                    _mediaPlayer.Play();
                        _isPlaying = true;
                    }
                    
                    // notify the UI about the change of the playback state
                    try
                    {
                        Debug.WriteLine("[AudioPlayerService] Invoking PlaybackStateChanged event");
                        PlaybackStateChanged?.Invoke(this, _isPlaying);
                    }
                    catch (Exception eventEx)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Error in PlaybackStateChanged event: {eventEx.GetType().FullName}: {eventEx.Message}");
                        Debug.WriteLine($"[AudioPlayerService] Stack trace: {eventEx.StackTrace}");
                    }
                }
                catch (InvalidCastException icEx)
                {
                    Debug.WriteLine($"[AudioPlayerService] InvalidCastException in TogglePlayPause: {icEx.Message}");
                    Debug.WriteLine($"[AudioPlayerService] Stack trace: {icEx.StackTrace}");
                }
                catch (Exception playbackEx)
                {
                    Debug.WriteLine($"[AudioPlayerService] Error controlling playback: {playbackEx.GetType().FullName}: {playbackEx.Message}");
                    Debug.WriteLine($"[AudioPlayerService] Stack trace: {playbackEx.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error toggling play/pause: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
            }
        }
        
        // playback of the next track in the playlist
        public void PlayNext()
        {
            try
            {
                if (Playlist == null || Playlist.Count == 0)
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot play next: Playlist is empty");
                    return;
                }
                
                // if the current index is not set or incorrect
                if (_currentIndex < 0 || _currentIndex >= Playlist.Count)
                {
                    Debug.WriteLine($"[AudioPlayerService] Current index out of range: {_currentIndex}, resetting to 0");
                    _currentIndex = 0;
                }
                else 
                {
                // determine the index of the next track
                    _currentIndex = (_currentIndex + 1) % Playlist.Count;
                }
                
                // checking the presence of a track by index before playback
                if (_currentIndex >= 0 && _currentIndex < Playlist.Count)
                {
                    var audio = Playlist[_currentIndex];
                    
                    if (audio != null)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Playing next track (index: {_currentIndex}): {audio.Artist} - {audio.Title}");
                        PlayAudio(audio);
                    }
                    else
                    {
                        Debug.WriteLine($"[AudioPlayerService] ERROR: Track at index {_currentIndex} is null");
                    }
                }
                else
                {
                    Debug.WriteLine($"[AudioPlayerService] ERROR: Cannot play next - index out of range: {_currentIndex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error playing next track: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
            }
        }
        
        // playback of the previous track in the playlist
        public void PlayPrevious()
        {
            try
            {
                if (Playlist == null || Playlist.Count == 0)
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot play previous: Playlist is empty");
                    return;
                }
                
                    // if the current position is more than 3 seconds, start the track from the beginning
                if (_mediaPlayer != null && _mediaPlayer.PlaybackSession != null && 
                    _mediaPlayer.PlaybackSession.Position.TotalSeconds > 3)
                {
                    _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
                    Debug.WriteLine("[AudioPlayerService] Restarting current track");
                    return;
                }
                
                // if the current index is not set or incorrect
                if (_currentIndex < 0 || _currentIndex >= Playlist.Count)
                {
                    Debug.WriteLine($"[AudioPlayerService] Current index out of range: {_currentIndex}, resetting to 0");
                    _currentIndex = 0;
                }
                else 
                {
                    // determine the index of the previous track
                    _currentIndex = (_currentIndex - 1 + Playlist.Count) % Playlist.Count;
                }
                
                // checking the presence of a track by index before playback
                if (_currentIndex >= 0 && _currentIndex < Playlist.Count)
                {
                    var audio = Playlist[_currentIndex];
                    
                    if (audio != null)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Playing previous track (index: {_currentIndex}): {audio.Artist} - {audio.Title}");
                        PlayAudio(audio);
                    }
                    else
                    {
                        Debug.WriteLine($"[AudioPlayerService] ERROR: Track at index {_currentIndex} is null");
                    }
                }
                else
                {
                    Debug.WriteLine($"[AudioPlayerService] ERROR: Cannot play previous - index out of range: {_currentIndex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error playing previous track: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[AudioPlayerService] Stack trace: {ex.StackTrace}");
            }
        }
        
        // setting the position of playback
        public void SetPosition(TimeSpan position)
        {
            try
            {
                if (_mediaPlayer?.PlaybackSession != null && _mediaPlayer.PlaybackSession.CanSeek)
                {
                    // check that the position is within the permissible limits
                    var duration = _mediaPlayer.PlaybackSession.NaturalDuration;
                    
                    if (position > duration)
                    {
                        position = duration;
                    }
                    else if (position < TimeSpan.Zero)
                    {
                        position = TimeSpan.Zero;
                    }
                    
                    // set the position
                    _mediaPlayer.PlaybackSession.Position = position;
                    
                    Debug.WriteLine($"[AudioPlayerService] Position set to {FormatTimeSpan(position)} of {FormatTimeSpan(duration)}");
                    
                    // notify the UI about the current position of playback
                    PositionChanged?.Invoke(this, position);
                }
                else
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot set position - player is not ready or cannot seek");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error setting position: {ex.Message}");
            }
        }
        
        // setting the volume of playback
        public void SetVolume(double volume)
        {
            try
            {
                // check that the volume is within the permissible limits (0-100)
                if (volume < 0)
                {
                    volume = 0;
                }
                else if (volume > 100)
                {
                    volume = 100;
                }
                
                // set the volume (0-1)
                _mediaPlayer.Volume = volume / 100.0;
                
                // notify the UI about the change of the volume
                VolumeChanged?.Invoke(this, volume);
                
                Debug.WriteLine($"[AudioPlayerService] Volume set to {volume}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error setting volume: {ex.Message}");
            }
        }
        
            // adding a track to favorites
        public async Task<bool> AddToFavorites(Models.Audio audio)
        {
            try
            {
                if (audio == null)
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot add null audio to favorites");
                    return false;
                }
                
                // get the access token
                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot add to favorites: Token is empty");
                    return false;
                }
                
                // form the URL of the request
                string apiUrl = $"method/audio.add?access_token={token}&v=5.126&audio_id={audio.Id}&owner_id={audio.OwnerId}";
                
                // perform the request
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var response = await httpClient.GetAsync(apiUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[AudioPlayerService] Add to favorites response: {content}");
                    
                    // update the flag "added to favorites"
                    audio.IsAdded = true;
                    
                    // if this is the current track, update it
                    if (CurrentAudio != null && CurrentAudio.Id == audio.Id && CurrentAudio.OwnerId == audio.OwnerId)
                    {
                        CurrentAudio.IsAdded = true;
                    }
                    
                    // update the status in the playlist, if it is there
                    var playlistTrack = Playlist.FirstOrDefault(a => a.Id == audio.Id && a.OwnerId == audio.OwnerId);
                    if (playlistTrack != null)
                    {
                        playlistTrack.IsAdded = true;
                    }
                    
                    // generate the event FavoriteStatusChanged
                    FavoriteStatusChanged?.Invoke(this, audio);
                    
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[AudioPlayerService] Error adding to favorites: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error adding to favorites: {ex.Message}");
                return false;
            }
        }
        
        // deleting a track from favorites
        public async Task<bool> RemoveFromFavorites(Models.Audio audio)
        {
            try
            {
                if (audio == null)
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot remove null audio from favorites");
                    return false;
                }
                
                // get the access token
                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot remove from favorites: Token is empty");
                    return false;
                }
                
                // form the URL of the request
                string apiUrl = $"method/audio.delete?access_token={token}&v=5.126&audio_id={audio.Id}&owner_id={audio.OwnerId}";
                
                // perform the request
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var response = await httpClient.GetAsync(apiUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[AudioPlayerService] Remove from favorites response: {content}");
                    
                    // update the flag "added to favorites"
                    audio.IsAdded = false;
                    
                    // if this is the current track, update it
                    if (CurrentAudio != null && CurrentAudio.Id == audio.Id && CurrentAudio.OwnerId == audio.OwnerId)
                    {
                        CurrentAudio.IsAdded = false;
                    }
                    
                    // update the status in the playlist, if it is there
                    var playlistTrack = Playlist.FirstOrDefault(a => a.Id == audio.Id && a.OwnerId == audio.OwnerId);
                    if (playlistTrack != null)
                    {
                        playlistTrack.IsAdded = false;
                    }
                    
                    // generate the event FavoriteStatusChanged
                    FavoriteStatusChanged?.Invoke(this, audio);
                    
                    return true;
                }
                else
                {
                    Debug.WriteLine($"[AudioPlayerService] Error removing from favorites: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error removing from favorites: {ex.Message}");
                return false;
            }
        }
        
        // getting audio records added to favorites (liked)
        public async Task<List<Models.Audio>> GetFavoriteAudioAsync()
        {
            try
            {
                Debug.WriteLine("[AudioPlayerService] Getting favorite audios");
                
                // get the access token
                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot get favorite audios: Token is empty");
                    return new List<Models.Audio>();
                }

                // get the ID of the current user
                int userId = await SessionHelper.GetUserIdAsync();
                if (userId == 0)
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot get favorite audios: User ID is not available");
                    return new List<Models.Audio>();
                }
                
                // form the URL of the request
                // the method audio.get with the parameter owner_id equal to the ID of the user returns favorite audio
                string apiUrl = $"method/audio.get?access_token={token}&v=5.126&owner_id={userId}&count=100";
                
                // perform the request
                var httpClient = await SessionHelper.GetHttpClientAsync();
                var response = await httpClient.GetAsync(apiUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[AudioPlayerService] Favorite audios response received, length: {content.Length} bytes");
                    
                    // convert the JSON response
                    var favoriteAudios = new List<Models.Audio>();
                    
                    try
                    {
                        // use JObject to parse JSON
                        var jsonResponse = JObject.Parse(content);
                        
                        // check the presence of the field response
                        if (jsonResponse["response"] == null)
                        {
                            Debug.WriteLine("[AudioPlayerService] Error: 'response' field not found in API response");
                            return favoriteAudios;
                        }
                        
                        // get the element response
                        var responseToken = jsonResponse["response"];
                        
                        // get the list of audio records
                        JArray itemsArray = null;
                        
                        // determine the format of the response (can be different)
                        if (responseToken["items"] != null && responseToken["items"].Type == JTokenType.Array)
                        {
                            // format: response.items[]
                            itemsArray = (JArray)responseToken["items"];
                            Debug.WriteLine($"[AudioPlayerService] Found {itemsArray.Count} favorite audio items");
                        }
                        else if (responseToken.Type == JTokenType.Array)
                        {
                            // format: response[]
                            itemsArray = (JArray)responseToken;
                            Debug.WriteLine($"[AudioPlayerService] Found {itemsArray.Count} favorite audio items (alternative format)");
                        }
                        
                        if (itemsArray == null || itemsArray.Count == 0)
                        {
                            Debug.WriteLine("[AudioPlayerService] No favorite audios found");
                            return favoriteAudios; // return an empty list, this is a normal situation
                        }
                        
                        // process each audio record
                        foreach (var item in itemsArray)
                        {
                            try
                            {
                                // create an object Audio from JSON
                                var audio = new Models.Audio
                                {
                                    Id = item["id"]?.Value<int>() ?? 0,
                                    OwnerId = item["owner_id"]?.Value<int>() ?? 0,
                                    Artist = item["artist"]?.Value<string>() ?? "Неизвестный исполнитель",
                                    Title = item["title"]?.Value<string>() ?? "Без названия",
                                    Duration = item["duration"]?.Value<int>() ?? 0,
                                    Url = item["url"]?.Value<string>() ?? string.Empty,
                                    IsAdded = true // if the track is in the list of favorites, then it is added
                                };
                                
                                // get the URL of the cover
                                string thumbUrl = ExtractThumbUrl(item);
                                if (!string.IsNullOrEmpty(thumbUrl))
                                {
                                    audio.ThumbUrl = thumbUrl;
                                }
                                
                                // add the audio record to the list
                                favoriteAudios.Add(audio);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[AudioPlayerService] Error processing audio item: {ex.Message}");
                                // continue processing other elements
                            }
                        }
                        
                        Debug.WriteLine($"[AudioPlayerService] Successfully processed {favoriteAudios.Count} favorite audios");
                        return favoriteAudios;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Error parsing favorite audios response: {ex.Message}");
                        return favoriteAudios;
                    }
                }
                else
                {
                    Debug.WriteLine($"[AudioPlayerService] Error getting favorite audios: {response.StatusCode}");
                    return new List<Models.Audio>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error in GetFavoriteAudioAsync: {ex.Message}");
                return new List<Models.Audio>();
            }
        }

        // auxiliary method for extracting the URL of the cover of the track
        private string ExtractThumbUrl(JToken audioItem)
        {
            try
            {
                // try to get the URL of the cover from different possible fields
                if (audioItem["album"] != null && audioItem["album"]["thumb"] != null)
                {
                    var thumb = audioItem["album"]["thumb"];
                    if (thumb["photo_300"] != null) return thumb["photo_300"].ToString();
                    if (thumb["photo_270"] != null) return thumb["photo_270"].ToString();
                    if (thumb["photo_135"] != null) return thumb["photo_135"].ToString();
                    if (thumb["photo_68"] != null) return thumb["photo_68"].ToString();
                }
                
                // OpenVK can also use the field "thumbs"
                if (audioItem["thumbs"] != null && audioItem["thumbs"].Type == JTokenType.Array)
                {
                    var thumbs = audioItem["thumbs"];
                    foreach (var thumb in thumbs)
                    {
                        if (thumb["photo"] != null) return thumb["photo"].ToString();
                    }
                }
                
                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error extracting thumb URL: {ex.Message}");
                return string.Empty;
            }
        }
        
        // handler of the event of opening media
        private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            Debug.WriteLine("[AudioPlayerService] Media opened successfully");
            
            // when opening media, try to get the actual duration of the track
            try
            {
                var duration = _mediaPlayer.PlaybackSession.NaturalDuration;
                
                // check if we have a valid duration
                if (duration.TotalSeconds <= 0 && _mediaPlayer.Source is MediaSource mediaSource && mediaSource.Duration.HasValue)
                {
                    duration = mediaSource.Duration.Value;
                }
                
                // log the duration of the track for diagnostics
                if (duration.TotalSeconds > 0)
                {
                    Debug.WriteLine($"[AudioPlayerService] Track duration: {FormatTimeSpan(duration)} ({duration.TotalSeconds:F1}s)");
                }
                else
                {
                    Debug.WriteLine("[AudioPlayerService] Warning: Could not determine track duration on media open");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error getting duration on media open: {ex.Message}");
            }
        }
        
        // handler of the event of the end of playback
        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine("[AudioPlayerService] Media ended, playing next track");
            PlayNext();
        }
        
        // handler of the event of the error of playback
        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Debug.WriteLine($"[AudioPlayerService] Media playback failed: {args.ErrorMessage}");
            _isPlaying = false;
            PlaybackStateChanged?.Invoke(this, _isPlaying);
            _positionTimer.Stop();
        }
        
        // handler of the event of the change of the state of playback
        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            try
            {
                switch (sender.PlaybackState)
                {
                    case MediaPlaybackState.Playing:
                        _isPlaying = true;
                        Debug.WriteLine("[AudioPlayerService] Playback state: Playing");
                        _positionTimer.Start(); // start the timer when playback starts
                        break;
                    case MediaPlaybackState.Paused:
                        _isPlaying = false;
                        Debug.WriteLine("[AudioPlayerService] Playback state: Paused");
                        _positionTimer.Stop(); // stop the timer when pause
                        break;
                    case MediaPlaybackState.None:
                    case MediaPlaybackState.Opening:
                    case MediaPlaybackState.Buffering:
                    default:
                        Debug.WriteLine($"[AudioPlayerService] Playback state: {sender.PlaybackState}");
                        break;
                }
                
                // notify the UI about the change of the state of playback
                PlaybackStateChanged?.Invoke(this, _isPlaying);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error in PlaybackStateChanged: {ex.Message}");
            }
        }
        
        // handler of the timer for tracking the position of playback
        private void PositionTimer_Tick(object sender, object e)
        {
            try
        {
            if (_mediaPlayer?.PlaybackSession != null && _isPlaying)
            {
                    var position = _mediaPlayer.PlaybackSession.Position;
                    var duration = _mediaPlayer.PlaybackSession.NaturalDuration;
                    
                    // additional check for the validity of values
                    if (position.TotalSeconds < 0)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Warning: Invalid position value: {position.TotalSeconds}s");
                        position = TimeSpan.Zero;
                    }
                    
                    if (duration.TotalSeconds <= 0)
                    {
                        Debug.WriteLine("[AudioPlayerService] Warning: Invalid duration value. Trying to get duration from media source.");
                        // in some cases, NaturalDuration may be unavailable, let's try to get it from other properties
                        try
                        {
                            if (_mediaPlayer.Source is MediaSource mediaSource && mediaSource.Duration.HasValue && mediaSource.Duration.Value.TotalSeconds > 0)
                            {
                                duration = mediaSource.Duration.Value;
                                Debug.WriteLine($"[AudioPlayerService] Got duration from media source: {FormatTimeSpan(duration)}");
                            }
                        }
                        catch (Exception durationEx)
                        {
                            Debug.WriteLine($"[AudioPlayerService] Error getting duration from media source: {durationEx.Message}");
                        }
                    }
                    
                    // if we have a valid duration of the track
                    if (duration.TotalSeconds > 0)
                    {
                        // check if the position exceeds the duration
                        if (position > duration)
                        {
                            position = duration;
                        }
                        
                        // detailed log for tracking the problem with the duration
                        if (position.TotalSeconds % 5 == 0 || duration.TotalMinutes >= 4) // for long tracks, we log more often
                        {
                            Debug.WriteLine($"[AudioPlayerService] Position update: {position.TotalSeconds:F1}s ({FormatTimeSpan(position)}) " +
                                            $"of {duration.TotalSeconds:F1}s ({FormatTimeSpan(duration)})");
                        }
                        
                        // update the UI with the current position
                        PositionChanged?.Invoke(this, position);
                    }
                    else
                    {
                        // if the duration is still unavailable, just pass the current position
                        PositionChanged?.Invoke(this, position);
                        Debug.WriteLine($"[AudioPlayerService] Position update with unknown duration: {FormatTimeSpan(position)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error in PositionTimer_Tick: {ex.Message}");
            }
        }
        
        // auxiliary method for formatting time
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // formatting time in the format MM:SS or H:MM:SS
            try
            {
                if (timeSpan.TotalSeconds < 0)
                    return "0:00";
                
                // for hours we use the format H:MM:SS
                if (timeSpan.Hours > 0)
                    return $"{timeSpan.Hours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                // for minutes always show seconds with two digits: M:SS
                else
                    return $"{timeSpan.Minutes}:{timeSpan.Seconds:D2}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error formatting timespan: {ex.Message}");
                return "0:00";
            }
        }
        
        // checking the status of the like for audio
        private async Task CheckAudioFavoriteStatusAsync(Models.Audio audio)
        {
            try
            {
                if (audio == null)
                {
                    Debug.WriteLine("[AudioPlayerService] Cannot check favorite status for null audio");
                    return;
                }
                
                Debug.WriteLine($"[AudioPlayerService] Checking favorite status for {audio.Artist} - {audio.Title} (ID: {audio.Id})");
                
                // check the status of the like for audio
                try
                {
                    bool isLiked = await SessionHelper.IsLikedAsync("audio", audio.OwnerId, audio.Id);
                    
                    // if the status has changed, update it and notify the UI
                    if (audio.IsAdded != isLiked)
                    {
                        Debug.WriteLine($"[AudioPlayerService] Audio favorite status updated: {isLiked}");
                        audio.IsAdded = isLiked;
                        FavoriteStatusChanged?.Invoke(this, audio);
                    }
                }
                catch (Exception ex)
                {
                    // if an error occurs when checking the status of the like, do not change the current status
                    Debug.WriteLine($"[AudioPlayerService] Error checking audio favorite status: {ex.Message}");
                    // do not call the FavoriteStatusChanged event, so as not to remove the track from the collection
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error in CheckAudioFavoriteStatusAsync: {ex.Message}");
            }
        }
        
        // freeing resources
        public void Dispose()
        {
            try
            {
                // stop the timer
                _positionTimer.Stop();
                
                // unsubscribe from events
                _mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
                _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
                _mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
                _mediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
                
                // free
                _mediaPlayer.Source = null;
                _mediaPlayer.Dispose();
                
                Debug.WriteLine("[AudioPlayerService] Disposed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error disposing: {ex.Message}");
            }
        }
    }
} 