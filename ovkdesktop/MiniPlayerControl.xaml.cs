using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Windows.Media.Playback;
using ovkdesktop.Models;

namespace ovkdesktop
{
    public sealed partial class MiniPlayerControl : UserControl
    {
        // service for managing audio playback
        private AudioPlayerService _audioService;
        
        // flag for tracking the update of the slider
        private bool _userIsSeeking = false;
        
        // flag for displaying the player
        private bool _isVisible = false;
        
        // constructor
        public MiniPlayerControl()
        {
            this.InitializeComponent();
            
            // initialization with the global service when creating
            Initialize(App.AudioService);
            
            // hide the player when initializing
            Hide();
            
            Debug.WriteLine("[MiniPlayerControl] Initialized");
        }
        
        // event of loading the control
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // if the audio service is not installed, initialize it
            if (_audioService == null)
            {
                Initialize(App.AudioService);
            }
        }
        
        // method for initialization with an explicit transfer of the service
        public void Initialize(AudioPlayerService audioService)
        {
            try
            {
                if (audioService == null)
                {
                    Debug.WriteLine("[MiniPlayerControl] Initialize: AudioService is null");
                    return;
                }
                
                // unsubscribe from old events if they were
                if (_audioService != null)
                {
                    _audioService.CurrentAudioChanged -= AudioService_CurrentAudioChanged;
                    _audioService.PlaybackStateChanged -= AudioService_PlaybackStateChanged;
                    _audioService.PositionChanged -= AudioService_PositionChanged;
                    _audioService.VolumeChanged -= AudioService_VolumeChanged;
                    _audioService.FavoriteStatusChanged -= AudioService_FavoriteStatusChanged;
                }
                
                // write the new service
                _audioService = audioService;
                
                // subscribe to events of the service
                _audioService.CurrentAudioChanged += AudioService_CurrentAudioChanged;
                _audioService.PlaybackStateChanged += AudioService_PlaybackStateChanged;
                _audioService.PositionChanged += AudioService_PositionChanged;
                _audioService.VolumeChanged += AudioService_VolumeChanged;
                _audioService.FavoriteStatusChanged += AudioService_FavoriteStatusChanged;
                
                    // set the initial volume and update the icon
                try 
                {
                    double volume = _audioService.GetVolume();
                    VolumeSlider.Value = volume;
                    UpdateVolumeIcon((float)(volume / 100.0));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MiniPlayerControl] Initialize: Error setting initial volume: {ex.Message}");
                }
                
                // if there is a current track, show the player, otherwise hide it
                if (_audioService.CurrentAudio != null && _audioService.IsPlaying)
                {
                    Show();
                    Debug.WriteLine("[MiniPlayerControl] Initialize: Showing mini player for current audio");
                    
                    // update UI with information about the current track
                    AudioService_CurrentAudioChanged(this, _audioService.CurrentAudio);
                }
                else
                {
                    Hide();
                    Debug.WriteLine("[MiniPlayerControl] Initialize: No current audio, hiding mini player");
                }
                
                // if the track is playing, update the icon
                AudioService_PlaybackStateChanged(this, _audioService.IsPlaying);
                
                Debug.WriteLine("[MiniPlayerControl] Initialize: Complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in Initialize: {ex.Message}");
            }
        }
        
        // show the player with animation
        public void Show()
        {
            try
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // first make the player visible, but with zero transparency (it will be animated)
                    if (this.Visibility != Visibility.Visible)
            {
                        this.Opacity = 0;
                        this.Visibility = Visibility.Visible;
                        _isVisible = true;
                        
                        // start the animation of appearance
                ShowPlayerStoryboard.Begin();
                        
                        Debug.WriteLine("[MiniPlayerControl] Show mini player with animation");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error showing mini player: {ex.Message}");
            }
        }
        
        // alias for the Show() method for compatibility
        public void ShowPlayer()
        {
            Show();
        }
        
        // hide the player with animation
        public void Hide()
        {
            try
            {
                if (!_isVisible) return; // already hidden
                
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // start the animation of hiding
                    HidePlayerStoryboard.Completed += (s, e) =>
                    {
                        this.Visibility = Visibility.Collapsed;
                _isVisible = false;
                    };
                    
                HidePlayerStoryboard.Begin();
                    Debug.WriteLine("[MiniPlayerControl] Hide mini player with animation");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error hiding mini player: {ex.Message}");
                
                        // backup option in case of animation error
                try
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        this.Visibility = Visibility.Collapsed;
                        _isVisible = false;
                    });
                }
                catch { }
            }
        }
        
        // handlers of events from AudioPlayerService
        
        private void AudioService_CurrentAudioChanged(object sender, Models.Audio audio)
        {
            try
            {
                if (_audioService == null || audio == null) return;
                
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try
                    {
                        // display information about the track
                        ArtistTextBlock.Text = audio.Artist;
                        TitleTextBlock.Text = audio.Title;
                        
                        // set the cover of the track
                        UpdateCoverImage(audio);
                        
                        // update the favorite icon
                        UpdateFavoriteButton(audio.IsAdded);
                        Debug.WriteLine($"[MiniPlayerControl] Track status: IsAdded={audio.IsAdded}");
                        
                        // show the mini player
                        if (_audioService.IsPlaying) 
                        {
                            Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MiniPlayer] UI update error: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayer] CurrentAudioChanged error: {ex.Message}");
            }
        }
        
        private void AudioService_PlaybackStateChanged(object sender, bool isPlaying)
        {
            try
            {
                if (_audioService == null) return;
                
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // update the icon depending on the playback state
                    PlayPauseIcon.Glyph = isPlaying ? "\uE769" : "\uE768";
                    
                    // show or hide the player depending on the state and the presence of the current track
                    if (isPlaying && _audioService.CurrentAudio != null)
                    {
                        Show();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error updating playback state: {ex.Message}");
            }
        }
        
        private void AudioService_PositionChanged(object sender, TimeSpan position)
        {
            try
            {
                if (_audioService == null) return;
                
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    try
                {
                    // skip the update if the user moves the slider
                        if (_userIsSeeking)
                        return;
                    
                    // get the total duration
                        TimeSpan duration = _audioService.TotalDuration;
                        
                        // check that there is a correct duration
                        if (duration.TotalSeconds <= 0)
                        {
                            Debug.WriteLine("[MiniPlayerControl] Warning: Invalid duration in AudioService_PositionChanged");
                            return;
                        }
                        
                        // format the time into strings
                        string formattedPosition = FormatTimeSpan(position);
                        string formattedDuration = FormatTimeSpan(duration);
                    
                        // update the text blocks
                        CurrentTimeTextBlock.Text = formattedPosition;
                        TotalTimeTextBlock.Text = formattedDuration;
                    
                        // set the slider to work with seconds directly
                        PositionSlider.Maximum = duration.TotalSeconds;
                        PositionSlider.Value = position.TotalSeconds;
                        
                        // only for debugging sometimes output information
                        if (position.Seconds % 30 == 0 || duration.TotalMinutes > 5)
                        {
                            Debug.WriteLine($"[MiniPlayerControl] Position: {formattedPosition} / {formattedDuration} ({position.TotalSeconds:F1}s / {duration.TotalSeconds:F1}s)");
                    }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MiniPlayerControl] Error updating position: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in AudioService_PositionChanged: {ex.Message}");
            }
        }
        
        private void AudioService_VolumeChanged(object sender, double volume)
        {
            try
            {
                // update the UI in the UI thread
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // update the value of the slider
                    VolumeSlider.Value = volume;
                    
                    // update the volume icon
                    UpdateVolumeIcon((float)(volume / 100.0));
                    
                    Debug.WriteLine($"[MiniPlayerControl] Volume changed: {volume}%");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error updating volume: {ex.Message}");
            }
        }
        
        private void AudioService_FavoriteStatusChanged(object sender, Models.Audio audio)
        {
            try
            {
                if (_audioService == null || audio == null) return;
                
                // check if the updated track is the currently playing one
                if (_audioService.CurrentAudio != null && 
                    _audioService.CurrentAudio.Id == audio.Id && 
                    _audioService.CurrentAudio.OwnerId == audio.OwnerId)
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        // update the favorite icon according to the new status
                        UpdateFavoriteButton(audio.IsAdded);
                        Debug.WriteLine($"[MiniPlayerControl] Updated favorite status to {audio.IsAdded} from external change");
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in AudioService_FavoriteStatusChanged: {ex.Message}");
            }
        }
        
        // handlers of UI events
        
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_audioService == null)
                {
                    Debug.WriteLine("[MiniPlayerControl] AudioPlayerService is null in PlayPauseButton_Click");
                    return;
                }
                
                _audioService.TogglePlayPause();
                Debug.WriteLine("[MiniPlayerControl] Play/Pause button clicked");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in PlayPauseButton_Click: {ex.Message}");
            }
        }
        
        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_audioService == null)
                {
                    Debug.WriteLine("[MiniPlayerControl] AudioPlayerService is null in PreviousButton_Click");
                    return;
                }
                
                _audioService.PlayPrevious();
                Debug.WriteLine("[MiniPlayerControl] Previous button clicked");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in PreviousButton_Click: {ex.Message}");
            }
        }
        
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_audioService == null)
                {
                    Debug.WriteLine("[MiniPlayerControl] AudioPlayerService is null in NextButton_Click");
                    return;
                }
                
                _audioService.PlayNext();
                Debug.WriteLine("[MiniPlayerControl] Next button clicked");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in NextButton_Click: {ex.Message}");
            }
        }
        
        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_audioService == null)
                {
                    Debug.WriteLine("[MiniPlayerControl] AudioPlayerService is null in FavoriteButton_Click");
                    return;
                }
                
                var currentAudio = _audioService.CurrentAudio;
                if (currentAudio == null)
                {
                    Debug.WriteLine("[MiniPlayerControl] No current audio to add/remove from favorites");
                    return;
                }
                
                bool success = false;
                
                if (currentAudio.IsAdded)
                {
                    // visual feedback before the request
                    FavoriteIcon.Glyph = "\uE00B"; // change the icon immediately to an empty heart
                    FavoriteIcon.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 128, G = 128, B = 128 }); // #808080 (gray)
                    
                    success = await _audioService.RemoveFromFavorites(currentAudio);
                    Debug.WriteLine($"[MiniPlayerControl] Remove from favorites result: {success}");
                    
                    // if the request failed, return the icon
                    if (!success)
                    {
                        FavoriteIcon.Glyph = "\uEB52"; // filled heart
                        FavoriteIcon.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 233, G = 30, B = 99 }); // #E91E63 (pink/red)
                    }
                }
                else
                {
                    // visual feedback before the request
                    FavoriteIcon.Glyph = "\uEB52"; // change the icon immediately to a filled heart
                    FavoriteIcon.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 233, G = 30, B = 99 }); // #E91E63 (pink/red)
                    
                    success = await _audioService.AddToFavorites(currentAudio);
                    Debug.WriteLine($"[MiniPlayerControl] Add to favorites result: {success}");
                    
                    // if the request failed, return the icon
                    if (!success)
                    {
                        FavoriteIcon.Glyph = "\uE00B"; // empty heart
                        FavoriteIcon.Foreground = new SolidColorBrush(new Windows.UI.Color { A = 255, R = 128, G = 128, B = 128 }); // #808080 (gray)
                    }
                }
                
                // update the UI after the operation, based on the actual status
                if (success)
                {
                    UpdateFavoriteButton(currentAudio.IsAdded);
                    Debug.WriteLine($"[MiniPlayerControl] Updated favorite status to {currentAudio.IsAdded}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in FavoriteButton_Click: {ex.Message}");
            }
        }
        
        private void PositionSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            try
            {
                // check the availability of the service
                if (_audioService == null) return;
                
                // if the user moves the slider, update only the text labels
                if (_userIsSeeking)
                {
                    // get the value in seconds
                    double seconds = e.NewValue;
                    
                    // create TimeSpan from seconds
                    TimeSpan currentPosition = TimeSpan.FromSeconds(seconds);
                
                    // format into a string
                    string formattedPosition = FormatTimeSpan(currentPosition);
                
                    // update only the text display of the current position
                    CurrentTimeTextBlock.Text = formattedPosition;
                
                    Debug.WriteLine($"[MiniPlayerControl] User seeking to {formattedPosition} ({seconds:F1}s)");
                }
                else
                {
                    // when changing programmatically, do nothing additional,
                    // because all updates occur in AudioService_PositionChanged
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in PositionSlider_ValueChanged: {ex.Message}");
            }
        }
        
        // handler of the change of the volume slider value
        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            try
            {
                if (_audioService == null) return;
                
                // get the new volume value
                double newVolume = e.NewValue;
                
                // set the volume in the audio service
                _audioService.SetVolume(newVolume);
                
                // update the volume icon
                UpdateVolumeIcon((float)(newVolume / 100.0));
                
                Debug.WriteLine($"[MiniPlayerControl] Volume slider changed to {newVolume}%");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in VolumeSlider_ValueChanged: {ex.Message}");
            }
        }
        
        // handler of the click on the slider
        private void PositionSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                _userIsSeeking = true;
                Debug.WriteLine("[MiniPlayerControl] User started seeking");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in PositionSlider_PointerPressed: {ex.Message}");
            }
        }
        
        // handler of the release of the slider
        private void PositionSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            try
            {
                if (_audioService == null) return;
                
                // USE THE VALUE OF THE SLIDER DIRECTLY AS SECONDS
                double seconds = PositionSlider.Value;
                TimeSpan newPosition = TimeSpan.FromSeconds(seconds);
                
                // set the position
                _audioService.SetPosition(newPosition);
                
                // update the text value of the time
                CurrentTimeTextBlock.Text = FormatTimeSpan(newPosition);
                
                Debug.WriteLine($"[MiniPlayerControl] Set position on release to {FormatTimeSpan(newPosition)} ({seconds:F1}s)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in PositionSlider_PointerReleased: {ex.Message}");
            }
            finally
            {
                _userIsSeeking = false;
            }
        }
        
        // handler of the tap on the slider for instant rewinding
        private void PositionSlider_Tapped(object sender, TappedRoutedEventArgs e)
        {
            try
            {
                if (_audioService == null) return;
                
                // get the total duration
                TimeSpan duration = _audioService.TotalDuration;
                
                // check the duration
                if (duration.TotalSeconds <= 0)
                {
                    Debug.WriteLine("[MiniPlayerControl] Warning: Cannot seek - track duration is zero");
                    return;
                }
                
                // get the position of the touch relative to the slider
                var point = e.GetPosition(PositionSlider);
                
                // calculate the size of the slider and take into account the position of the control
                var sliderWidth = PositionSlider.ActualWidth;
                var ratio = point.X / sliderWidth;
                
                // limit the value within 0-1
                ratio = Math.Clamp(ratio, 0, 1);
                
                // calculate seconds directly
                double seconds = ratio * duration.TotalSeconds;
                
                // set the value of the slider in seconds
                PositionSlider.Value = seconds;
                
                // create a new position
                TimeSpan newPosition = TimeSpan.FromSeconds(seconds);
                
                // format the display of time
                string formattedPosition = FormatTimeSpan(newPosition);
                string formattedDuration = FormatTimeSpan(duration);
                
                // update the text display of time
                CurrentTimeTextBlock.Text = formattedPosition;
                
                // set the position
                _audioService.SetPosition(newPosition);
                
                Debug.WriteLine($"[MiniPlayerControl] Tapped to position {formattedPosition} of {formattedDuration} ({seconds:F2}s)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in PositionSlider_Tapped: {ex.Message}");
            }
        }
        
        // add a handler of the click on the volume button
        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_audioService == null) return;
                
                // get the current volume
                double currentVolume = _audioService.GetVolume();
                
                // set the new volume value (switching between full volume and no sound)
                double newVolume = currentVolume > 0 ? 0 : 100;
                _audioService.SetVolume(newVolume);
                
                // update the value of the slider
                VolumeSlider.Value = newVolume;
                
                // update the icon
                UpdateVolumeIcon((float)(newVolume / 100.0));
                
                Debug.WriteLine($"[MiniPlayerControl] Volume toggled from {currentVolume} to {newVolume}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error in VolumeButton_Click: {ex.Message}");
            }
        }
        
        // auxiliary methods
        
        private void UpdateCoverImage(Models.Audio audio)
        {
            try
            {
                if (audio != null)
                {
                    if (!string.IsNullOrEmpty(audio.ThumbUrl))
                    {
                    CoverImage.Source = new BitmapImage(new Uri(audio.ThumbUrl));
                        DefaultMusicIcon.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        CoverImage.Source = null;
                        DefaultMusicIcon.Visibility = Visibility.Visible;
                    }
                    Debug.WriteLine("[MiniPlayerControl] Updated cover image");
                }
                else
                {
                    // set the default cover if the audio is not set
                    CoverImage.Source = null;
                    DefaultMusicIcon.Visibility = Visibility.Visible;
                    Debug.WriteLine("[MiniPlayerControl] Set default cover image (no audio)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error updating cover image: {ex.Message}");
                
                // if an error occurs, simply show the music icon
                try
                {
                    CoverImage.Source = null;
                    DefaultMusicIcon.Visibility = Visibility.Visible;
                }
                catch
                {
                    // ignore the error
                }
            }
        }
        
        private void UpdateFavoriteButton(bool isAdded)
        {
            // update the favorite icon (EB52 - filled heart, E00B - empty heart)
            FavoriteIcon.Glyph = isAdded ? "\uEB52" : "\uE00B";
            
            // set the color of the icon: red for liked, gray for not liked
            FavoriteIcon.Foreground = isAdded 
                ? new SolidColorBrush(new Windows.UI.Color { A = 255, R = 233, G = 30, B = 99 })  // #E91E63 (pink/red)
                : new SolidColorBrush(new Windows.UI.Color { A = 255, R = 128, G = 128, B = 128 }); // #808080 (gray)
            
            Debug.WriteLine($"[MiniPlayerControl] Updated favorite icon: {(isAdded ? "Added" : "Not added")}");
        }

        // update the volume icon depending on the level
        private void UpdateVolumeIcon(float volumeLevel)
        {
            try
            {
                if (volumeLevel > 0.7f)
                    VolumeIcon.Glyph = "\uE767"; // high volume 

                else if (volumeLevel > 0.1f)
                    VolumeIcon.Glyph = "\uE995"; // medium volume 

                else if (volumeLevel > 0)
                    VolumeIcon.Glyph = "\uE992"; // low volume 

                else
                    VolumeIcon.Glyph = "\uE74F"; // no sound (Mute)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MiniPlayerControl] Error updating volume icon: {ex.Message}");
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            // formatting the time in the format MM:SS or H:MM:SS
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
                Debug.WriteLine($"[MiniPlayerControl] Error formatting timespan: {ex.Message}");
                return "0:00";
            }
        }
    }
} 