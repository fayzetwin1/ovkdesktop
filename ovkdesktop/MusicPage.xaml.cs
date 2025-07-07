using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using ovkdesktop.Models;
using Microsoft.UI.Dispatching;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI;

namespace ovkdesktop
{
    // extension class for DispatcherQueue
    public static class DispatcherQueueExtensions
    {
        // asynchronous wrapper for TryEnqueue
        public static Task<bool> TryEnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue dispatcherQueue, 
            Microsoft.UI.Dispatching.DispatcherQueuePriority priority, Microsoft.UI.Dispatching.DispatcherQueueHandler callback)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            
            bool result = dispatcherQueue.TryEnqueue(priority, () =>
            {
                try
                {
                    callback();
                    taskCompletionSource.SetResult(true);
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });
            
            if (!result)
            {
                taskCompletionSource.SetResult(false);
            }
            
            return taskCompletionSource.Task;
        }
    }

    /// <summary>
    /// Page for displaying and playing audio records.
    /// </summary>
    public sealed partial class MusicPage : Page
    {
        private HttpClient httpClient;
        private string instanceUrl;
        private enum AudioMode
        {
            Popular,
            MyAudio,
            Search
        }
        
        private ObservableCollection<Models.Audio> _myAudioCollection = new ObservableCollection<Models.Audio>();
        private ObservableCollection<Models.Audio> _popularAudioCollection = new ObservableCollection<Models.Audio>();
        private ObservableCollection<Models.Audio> _recommendedAudioCollection = new ObservableCollection<Models.Audio>();
        private readonly AudioPlayerService _audioService;
        private bool _userIsSeeking = false;
        private AudioMode _currentMode = AudioMode.Popular;
        private string _searchQuery = string.Empty;

        public MusicPage()
        {
            this.InitializeComponent();
            
            // get instance of audio service
            _audioService = App.AudioService;
            
            // set initial mode
            _currentMode = AudioMode.Popular;
            
            // subscribe to events
            if (_audioService != null)
            {
                _audioService.FavoriteStatusChanged += AudioService_FavoriteStatusChanged;
            }
            
            // set data sources for lists
            MyAudioListView.ItemsSource = _myAudioCollection;
            PopularAudioListView.ItemsSource = _popularAudioCollection;
            RecommendedAudioListView.ItemsSource = _recommendedAudioCollection;
            
            // load data when page is loaded
            this.Loaded += MusicPage_Loaded;
            this.Unloaded += MusicPage_Unloaded;
            
            // add handlers for search
            SearchButtonInTab.Click += SearchButtonInTab_Click;
            SearchBoxInTab.KeyDown += SearchBoxInTab_KeyDown;
            
            Debug.WriteLine("[MusicPage] Initialized");
        }

        private async void MusicPage_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeHttpClientAsync();
            await LoadPopularAudio();
        }

        private async Task InitializeHttpClientAsync()
        {
            try
            {
                // get instance URL from settings
                instanceUrl = await SessionHelper.GetInstanceUrlAsync();
                httpClient = await SessionHelper.GetConfiguredHttpClientAsync();
                
                Debug.WriteLine($"[MusicPage] Initialized with instance URL: {instanceUrl}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error initializing: {ex.Message}");
                ShowError($"Error initializing: {ex.Message}");
            }
        }
        
        private async Task<OVKDataBody> LoadTokenAsync()
        {
            try
            {
                if (!File.Exists("ovkdata.json"))
                {
                    ShowError("Unauthorized");
                    return null;
                }
                
                using var fs = new FileStream("ovkdata.json", FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(fs);
                var jsonContent = await reader.ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<OVKDataBody>(jsonContent);
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error loading token: {ex.Message}");
                ShowError($"Error loading token: {ex.Message}");
                return null;
            }
        }

        // add classes for deserialization of API response
        public class AudioResponse
        {
            public int Count { get; set; }
            public List<AudioItem> Items { get; set; } = new List<AudioItem>();
        }

        public class AudioItem
        {
            public string Unique_id { get; set; }
            public int Aid { get; set; }
            public int Id { get; set; }
            public string Artist { get; set; }
            public string Title { get; set; }
            public int Duration { get; set; }
            public string Url { get; set; }
            public AlbumInfo Album { get; set; }
            public bool? Added { get; set; }
            public bool? Is_added { get; set; }
        }

        public class AlbumInfo
        {
            public ThumbInfo Thumb { get; set; }
        }

        public class ThumbInfo
        {
            public string Photo_300 { get; set; }
            public string Photo_270 { get; set; }
            public string Photo_135 { get; set; }
            public string Photo_68 { get; set; }
        }

        private async Task LoadPopularAudio()
        {
            try
            {
                LoadingProgressRing.IsActive = true;
                
                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    ShowError("Unable to get access token");
                    return;
                }
                
                var httpClient = await SessionHelper.GetHttpClientAsync();
                string apiUrl = $"method/audio.getPopular?access_token={token}&v=5.126&count=100";
                
                Debug.WriteLine("[MusicPage] Requesting popular audio from API...");
                
                var response = await httpClient.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ShowError($"Request error: {response.StatusCode}");
                    return;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[MusicPage] Popular audio response received (length: {content.Length})");
                
                try
                {
                    // parse JSON using Newtonsoft.Json for greater flexibility
                    JObject jsonObject = JObject.Parse(content);
                    JObject responseObj = jsonObject["response"] as JObject;
                    
                    if (responseObj == null)
                    {
                        Debug.WriteLine("[MusicPage] Response object is null or not a JSON object");
                        ShowError("Invalid API response");
                        return;
                    }
                    
                    // get array "items"
                    JArray itemsArray = responseObj["items"] as JArray;
                    if (itemsArray == null || !itemsArray.Any())
                    {
                        Debug.WriteLine("[MusicPage] Items array is empty or null");
                        return;
                    }
                    
                    Debug.WriteLine($"[MusicPage] Found {itemsArray.Count} items in popular audio");
                    _popularAudioCollection.Clear();
                    
                    // process each element of array
                    foreach (JToken item in itemsArray)
                    {
                        try
                        {
                            Debug.WriteLine($"[MusicPage] Processing item type: {item.Type}");
                            
                            // convert JToken to Audio
                            var audio = CreateAudioFromJToken(item);
                            
                            if (audio != null)
                            {
                                Debug.WriteLine($"[MusicPage] Adding to popular collection: {audio.Artist} - {audio.Title} (ID: {audio.Id})");
                                _popularAudioCollection.Add(audio);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[MusicPage] Error processing item: {ex.GetType().FullName}: {ex.Message}");
                            Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                        }
                    }
                    
                    Debug.WriteLine($"[MusicPage] Loaded {_popularAudioCollection.Count} popular audio tracks");
                    
                    // update UI
                    _currentMode = AudioMode.Popular;
                    Debug.WriteLine($"[MusicPage] Current mode changed to: Popular");
                    
                    // show corresponding list
                    // update visibility of lists depending on mode
                    if (_currentMode == AudioMode.Popular)
                    {
                        UpdateUIVisibility(_popularAudioCollection.Count > 0);
                        
                        // show tab with popular music
                        if (AudioPivot.SelectedIndex != 1)
                            AudioPivot.SelectedIndex = 1;
                    }
                    
                    // detailed debug of collections
                    DebugCollections();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MusicPage] Error parsing response: {ex.GetType().FullName}: {ex.Message}");
                    Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                    ShowError($"Data processing error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in LoadPopularAudio: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                ShowError($"Error loading popular audio: {ex.Message}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
            }
        }
        
        // helper method for creating Audio from JToken
        private Models.Audio CreateAudioFromJToken(Newtonsoft.Json.Linq.JToken token)
        {
            try
            {
                Debug.WriteLine($"[MusicPage] CreateAudioFromJToken: Processing token type {token?.Type.ToString() ?? "null"}");
                
                // use static method from Audio class
                Models.Audio audio = Models.Audio.FromJToken(token);
                
                if (audio != null)
                {
                    Debug.WriteLine($"[MusicPage] Successfully created Audio from JToken: {audio.Artist} - {audio.Title} (ID: {audio.Id})");
                }
                else
                {
                    Debug.WriteLine("[MusicPage] Failed to create Audio from JToken");
                }
                
                return audio;
                        }
                        catch (Exception ex)
                        {
                Debug.WriteLine($"[MusicPage] Error in CreateAudioFromJToken: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                return null;
            }
        }
        
        private async Task LoadMyAudio()
        {
            try
            {
                LoadingProgressRing.IsActive = true;
                
                // clear collection before adding new elements
                _myAudioCollection.Clear();
                
                // use new method in AudioPlayerService to get liked audio records
                var favoriteAudios = await _audioService.GetFavoriteAudioAsync();
                
                if (favoriteAudios != null && favoriteAudios.Count > 0)
                {
                    Debug.WriteLine($"[MusicPage] Loaded {favoriteAudios.Count} favorite audio tracks");
                    
                    // add all elements to collection
                    foreach (var audio in favoriteAudios)
                    {
                        _myAudioCollection.Add(audio);
                    }
                    
                    // update counter and header
                    MyAudioHeader.Text = $"Мои треки ({_myAudioCollection.Count})";
                    
                    // update visibility of interface elements
                    UpdateUIVisibility(true);
                }
                else
                {
                    Debug.WriteLine("[MusicPage] No favorite audio tracks found");
                    
                    // update header with information about the absence of tracks
                    MyAudioHeader.Text = "You have no saved audios";
                    
                    // update visibility of interface elements
                    UpdateUIVisibility(false);
                    
                    // do not show error message, because this is a normal situation
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error loading favorite audios: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                ShowError($"Error loading audios: {ex.Message}");
            }
            finally
            {
                LoadingProgressRing.IsActive = false;
                MyAudioLoadingRing.IsActive = false; // disable loading indicator for "My Audio" section
            }
        }
        
        private void HideError()
        {
            // method left for backward compatibility
        }

        private void AudioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Models.Audio audio)
            {
                PlaySelectedAudio(audio);
            }
        }
        
        private void AudioList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Models.Audio audio)
            {
                PlaySelectedAudio(audio);
            }
        }

        private void PlaySelectedAudio(Models.Audio audio)
        {
            try
            {
                if (audio == null)
                {
                    Debug.WriteLine("[MusicPage] ERROR: Attempted to play null audio");
                    return;
                }
                
                // output information for debugging
                Debug.WriteLine($"[MusicPage] Playing: {audio.Artist} - {audio.Title} (ID: {audio.Id})");
                Debug.WriteLine($"[MusicPage] Audio URL: {(string.IsNullOrEmpty(audio.Url) ? "NULL/EMPTY" : audio.Url.Substring(0, Math.Min(50, audio.Url.Length)) + "...")}");
                
                try
                {
                    // determine current collection depending on mode
                    ObservableCollection<Models.Audio> currentPlaylist = null;
                
                switch (_currentMode)
                {
                    case AudioMode.MyAudio:
                        currentPlaylist = _myAudioCollection;
                        break;
                    case AudioMode.Popular:
                        currentPlaylist = _popularAudioCollection;
                        break;
                    case AudioMode.Search:
                        currentPlaylist = _recommendedAudioCollection;
                        break;
                        default:
                            currentPlaylist = _popularAudioCollection;
                        break;
                }
                
                    // check that playlist is not empty
                    if (currentPlaylist == null || currentPlaylist.Count == 0)
                    {
                        Debug.WriteLine("[MusicPage] Current playlist is empty, using single track mode");
                        var singleTrackList = new ObservableCollection<Models.Audio> { audio };
                        _audioService.SetPlaylist(singleTrackList, 0);
                        return;
                    }
                    
                    // additional logging of playlist content
                    Debug.WriteLine($"[MusicPage] Current playlist count: {currentPlaylist.Count}");
                    
                    // find index of selected track in playlist
                    int index = FindAudioIndexInCollection(audio, currentPlaylist);
                    
                    // set playlist and start playback
                    if (index >= 0)
                    {
                        Debug.WriteLine($"[MusicPage] Using playlist with index {index}");
                        _audioService.SetPlaylist(currentPlaylist, index);
                    }
                    else
                    {
                        // if track is not found in playlist, play it separately
                        Debug.WriteLine("[MusicPage] Track not found in playlist, using single track mode");
                        var singleTrackList = new ObservableCollection<Models.Audio> { audio };
                        _audioService.SetPlaylist(singleTrackList, 0);
                    }
                }
                catch (InvalidCastException icex)
                {
                    Debug.WriteLine($"[MusicPage] InvalidCastException in PlaySelectedAudio: {icex.Message}");
                    Debug.WriteLine($"[MusicPage] Stack trace: {icex.StackTrace}");
                    // log additional information about the object that caused the exception
                    Debug.WriteLine($"[MusicPage] Audio object: {audio?.GetType().FullName ?? "null"}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MusicPage] Error in PlaySelectedAudio: {ex.GetType().FullName}: {ex.Message}");
                    Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in PlaySelectedAudio: {ex.Message}");
            }
        }
        

        
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioService != null)
            {
                _audioService.TogglePlayPause();
            }
        }
        
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioService != null)
            {
                _audioService.PlayPrevious();
            }
        }
        
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioService != null)
            {
                _audioService.PlayNext();
            }
        }
        
        private void PositionSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
                    {
                        try
                        {
                _userIsSeeking = true;
                Debug.WriteLine("[MusicPage] User started seeking");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in PositionSlider_PointerPressed: {ex.Message}");
            }
        }
        
        
        
        
        
        
        
        private void VolumeSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_audioService != null)
            {
                _audioService.SetVolume(e.NewValue);
            }
        }

        // setting current mode
        private void SetCurrentMode(AudioMode mode)
        {
            try
            {
                Debug.WriteLine($"[MusicPage] Current mode changed to: {mode}");
                _currentMode = mode;
                
                // update page header depending on mode
                switch (mode)
                {
                    case AudioMode.MyAudio:
                        MyAudioHeader.Text = $"My audios ({_myAudioCollection.Count})";
                        break;
                    case AudioMode.Popular:
                        // Popular already has its own header
                        break;
                    case AudioMode.Search:
                        // Header for search will be updated when search is performed
                        break;
                }
                
                // debug of collections
                DebugCollections();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error setting current mode: {ex.GetType().FullName}: {ex.Message}");
            }
        }

        private void AudioPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[MusicPage] AudioPivot selected index: {AudioPivot.SelectedIndex}");
                
                switch (AudioPivot.SelectedIndex)
                {
                    case 0: // My Audio
                        if (_myAudioCollection.Count == 0)
                        {
                            // activate loading indicator for this section
                            MyAudioLoadingRing.IsActive = true;
                            _ = LoadMyAudio();
                        }
                        SetCurrentMode(AudioMode.MyAudio);
                                            break;
                        
                    case 1: // Popular
                        if (_popularAudioCollection.Count == 0)
                        {
                            // activate loading indicator for this section
                            PopularLoadingRing.IsActive = true;
                            _ = LoadPopularAudio();
                        }
                        SetCurrentMode(AudioMode.Popular);
                        break;
                        
                    case 2: // Search (former section "Recommendations")
                        // if we already have search results, show them
                        if (!string.IsNullOrEmpty(_searchQuery) && _recommendedAudioCollection.Count > 0)
                        {
                            Debug.WriteLine($"[MusicPage] Showing existing search results for: {_searchQuery}");
                        }
                        // otherwise load recommendations
                        else
                        {
                            // activate loading indicator for this section
                            SearchLoadingRing.IsActive = true;
                            _ = LoadRecommendedAudio();
                            Debug.WriteLine("[MusicPage] Loading recommended audio");
                        }
                        break;
                }
                
                DebugCollections(); // for debugging
                        }
                        catch (Exception ex)
                        {
                Debug.WriteLine($"[MusicPage] Error in AudioPivot_SelectionChanged: {ex.Message}");
                
                // in case of error, disable all loading indicators
                MyAudioLoadingRing.IsActive = false;
                PopularLoadingRing.IsActive = false;
                SearchLoadingRing.IsActive = false;
            }
        }
        
        // handler of refresh button click
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AudioPivot.SelectedIndex == 0)
                {
                    // activate loading indicator for "My Audio" section
                    MyAudioLoadingRing.IsActive = true;
                    // update my audio
                    _ = LoadMyAudio();
                }
                else if (AudioPivot.SelectedIndex == 1)
                {
                    // activate loading indicator for "Popular" section
                    PopularLoadingRing.IsActive = true;
                    // update popular audio
                    _ = LoadPopularAudio();
                }
                else if (AudioPivot.SelectedIndex == 2)
                {
                    // if there is a search query, repeat search
                    if (!string.IsNullOrEmpty(_searchQuery))
                    {
                        // activate loading indicator for "Search" section
                        SearchLoadingRing.IsActive = true;
                        _ = SearchAudio(_searchQuery);
                    }
                    // otherwise load recommendations
                    else
                    {
                        // activate loading indicator for "Search" section
                        SearchLoadingRing.IsActive = true;
                        _ = LoadRecommendedAudio();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in RefreshButton_Click: {ex.Message}");
                // in case of error, disable all loading indicators
                MyAudioLoadingRing.IsActive = false;
                PopularLoadingRing.IsActive = false;
                SearchLoadingRing.IsActive = false;
            }
        }
        
        // handler of "Favorite" button click
        private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // check availability of service
                if (_audioService == null)
                {
                    Debug.WriteLine("[MusicPage] AudioPlayerService is null in FavoriteButton_Click");
                    ShowError("Service unavailable");
                    return;
                }
                
                // get audio from button context
                var button = sender as Button;
                if (button == null || button.DataContext == null)
                {
                    Debug.WriteLine("[MusicPage] Button or DataContext is null in FavoriteButton_Click");
                    ShowError("Unable to get track data");
                    return;
                }
                
                var audio = button.DataContext as Models.Audio;
                if (audio == null)
                {
                    Debug.WriteLine("[MusicPage] Audio is null in FavoriteButton_Click");
                    ShowError("Invalid track data");
                    return;
                }
                
                Debug.WriteLine($"[MusicPage] FavoriteButton_Click for {audio.Artist} - {audio.Title}");
                Debug.WriteLine($"[MusicPage] Current favorite status: {audio.IsAdded}");
                
                // first update UI for immediate response
                // invert status (temporarily)
                bool wasAdded = audio.IsAdded;
                audio.IsAdded = !audio.IsAdded;
                
                // update button icon
                var icon = button.Content as FontIcon;
                if (icon != null)
                {
                    icon.Glyph = audio.IsAdded ? "\uEB52" : "\uE734"; // filled/empty heart
                    icon.Foreground = audio.IsAdded ? 
                        new SolidColorBrush(Windows.UI.Color.FromArgb(255, 233, 30, 99)) : 
                        new SolidColorBrush(Microsoft.UI.Colors.White);
                }
                
                // if this is "My Audio" section and user removes track from favorites,
                // remove it from collection
                if (_currentMode == AudioMode.MyAudio && !audio.IsAdded)
                {
                    // find track in collection
                    int index = FindAudioIndexInCollection(audio, _myAudioCollection);
                    if (index >= 0)
                    {
                        // remember position for updating UI
                        int trackIndex = index;
                        
                        // remove from collection (there will be a little animation when removing)
                        await ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => 
                        {
                            _myAudioCollection.RemoveAt(trackIndex);
                        });
                    }
                }
                
                // then send request to server
                bool success = false;
                
                try
                {
                    if (wasAdded)
                    {
                        // remove from favorites
                        success = await _audioService.RemoveFromFavorites(audio);
                        Debug.WriteLine($"[MusicPage] Remove from favorites result: {success}");
                    }
                    else
                    {
                        // add to favorites
                        success = await _audioService.AddToFavorites(audio);
                        Debug.WriteLine($"[MusicPage] Add to favorites result: {success}");
                    }
                    
                    // if operation is not successful, rollback UI changes
                    if (!success)
                    {
                        // return to original state
                        audio.IsAdded = wasAdded;
                        
                        // rollback icon
                        if (icon != null)
                        {
                            icon.Glyph = wasAdded ? "\uEB52" : "\uE734";
                            icon.Foreground = wasAdded ? 
                                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 233, 30, 99)) : 
                                new SolidColorBrush(Microsoft.UI.Colors.White);
                        }
                        
                        // show error
                        Debug.WriteLine("[MusicPage] FavoriteButton_Click: Operation failed");
                        ShowError("Unable to update favorite status");
                    }
                        }
                        catch (Exception ex)
                        {
                    // in case of any error, rollback UI changes
                    audio.IsAdded = wasAdded;
                    if (icon != null)
                    {
                        icon.Glyph = wasAdded ? "\uEB52" : "\uE734";
                        icon.Foreground = wasAdded ? 
                            new SolidColorBrush(Windows.UI.Color.FromArgb(255, 233, 30, 99)) : 
                            new SolidColorBrush(Microsoft.UI.Colors.White);
                    }
                    Debug.WriteLine($"[MusicPage] Error in favorite operation: {ex.Message}");
                    ShowError($"Error updating favorite: {ex.Message}");
                    return;
                }
                
                // update current track in service, if it is
                if (_audioService?.CurrentAudio != null && 
                    _audioService.CurrentAudio.Id == audio.Id && 
                    _audioService.CurrentAudio.OwnerId == audio.OwnerId)
                {
                    _audioService.CurrentAudio.IsAdded = audio.IsAdded;
                }
                
                // update header, if we are in "My Audio" section
                if (_currentMode == AudioMode.MyAudio)
                {
                    MyAudioHeader.Text = $"My audios ({_myAudioCollection.Count})";
                }
            }
            catch (InvalidCastException icex)
            {
                Debug.WriteLine($"[MusicPage] InvalidCastException in FavoriteButton_Click: {icex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {icex.StackTrace}");
                ShowError("Error updating favorite");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in FavoriteButton_Click: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
                ShowError($"Error: {ex.Message}");
            }
        }
        
            // method for displaying error message
        private void ShowError(string message)
        {
            Debug.WriteLine($"[MusicPage] Error: {message}");
            
            // show error message in UI thread
            ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueue(async () =>
            {
                try
                {
                    ContentDialog errorDialog = new ContentDialog()
                    {
                        Title = "Error",
                        Content = message,
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    
                    await errorDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MusicPage] Error showing error dialog: {ex.Message}");
                }
            });
        }
        
        // methods for AudioService
        public void Play()
        {
            try
            {
                if (_audioService != null && _audioService.CurrentAudio != null)
                {
                    Debug.WriteLine("[MusicPage] Play method called with current audio: " + 
                        _audioService.CurrentAudio.Artist + " - " + _audioService.CurrentAudio.Title);
                    
                    // if we already have current audio, simply continue playback
                    if (_audioService.IsPlaying)
                    {
                        Debug.WriteLine("[MusicPage] Audio is already playing");
                    }
                    else
                    {
                        Debug.WriteLine("[MusicPage] Starting playback of current audio");
                        _audioService.TogglePlayPause();
                    }
                }
                else
                {
                    Debug.WriteLine("[MusicPage] No current audio to play");
                    
                    // if there is no current audio, select first track from current collection
                    ObservableCollection<Models.Audio> currentPlaylist = null;
                
                switch (_currentMode)
                {
                    case AudioMode.MyAudio:
                        currentPlaylist = _myAudioCollection;
                        break;
                    case AudioMode.Popular:
                        currentPlaylist = _popularAudioCollection;
                        break;
                    case AudioMode.Search:
                        currentPlaylist = _recommendedAudioCollection;
                        break;
                        default:
                            currentPlaylist = _popularAudioCollection;
                        break;
                }
                
                    // check that playlist is not empty
                    if (currentPlaylist != null && currentPlaylist.Count > 0)
                    {
                        var firstAudio = currentPlaylist[0];
                        Debug.WriteLine($"[MusicPage] Playing first track from current playlist: {firstAudio.Artist} - {firstAudio.Title}");
                        _audioService.SetPlaylist(currentPlaylist, 0);
                    }
                    else
                    {
                        Debug.WriteLine("[MusicPage] Current playlist is empty, cannot play");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in Play method: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
            }
        }
        
        public void Pause()
        {
            if (_audioService != null)
            {
                _audioService.TogglePlayPause();
            }
        }
        
        public void TogglePlayPause()
        {
            if (_audioService != null)
            {
                _audioService.TogglePlayPause();
            }
        }
        
        public void PlayNext()
        {
            if (_audioService != null)
            {
                _audioService.PlayNext();
            }
        }
        
        public void PlayPrevious()
        {
            if (_audioService != null)
            {
                _audioService.PlayPrevious();
            }
        }
        
        // method SafeCastToAudio is no longer needed, because replaced by static method Models.Audio.SafeCast

        // method for debugging collections
        private void DebugCollections()
        {
            try
            {
                Debug.WriteLine("[MusicPage] Debugging collections...");
                
                // Popular
                Debug.WriteLine($"[MusicPage] Popular collection count: {_popularAudioCollection.Count}");
                if (_popularAudioCollection.Count > 0)
                {
                    var firstItem = _popularAudioCollection[0];
                    var secondItem = _popularAudioCollection.Count > 1 ? _popularAudioCollection[1] : null;
                    
                    Debug.WriteLine($"[MusicPage] First popular item type: {firstItem?.GetType().FullName ?? "null"}");
                    Debug.WriteLine($"[MusicPage] First popular item: {firstItem?.Artist} - {firstItem?.Title}");
                    if (firstItem != null)
                    {
                        Debug.WriteLine($"[MusicPage] First popular item ID: {firstItem.Id}, OwnerId: {firstItem.OwnerId}");
                    }
                    
                    if (secondItem != null)
                    {
                        Debug.WriteLine($"[MusicPage] Second popular item type: {secondItem.GetType().FullName}");
                        Debug.WriteLine($"[MusicPage] Second popular item: {secondItem.Artist} - {secondItem.Title}");
                    }
                }
                
                    // My Audio
                Debug.WriteLine($"[MusicPage] My audio collection count: {_myAudioCollection.Count}");
                
                // Recommended
                Debug.WriteLine($"[MusicPage] Recommended collection count: {_recommendedAudioCollection.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in DebugCollections: {ex.GetType().FullName}: {ex.Message}");
            }
        }
        
        // method for displaying/hiding UI elements depending on the presence of data
        private void UpdateUIVisibility(bool hasItems = false)
        {
            try
            {
                // explicitly specify Microsoft.UI.Dispatching
                ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // always hide loading indicator if there are items
                    if (hasItems)
                    {
                        LoadingProgressRing.IsActive = false;
                        return;
                    }
                    
                    // update text if there are no items
                    switch (_currentMode)
                    {
                        case AudioMode.MyAudio:
                            // for "My Audio" section text is already updated in LoadMyAudio
                            // disable loading indicator
                            LoadingProgressRing.IsActive = false;
                            break;
                        case AudioMode.Popular:
                            // disable loading indicator
                            LoadingProgressRing.IsActive = false;
                            break;
                        case AudioMode.Search:
                            // disable loading indicator
                            LoadingProgressRing.IsActive = false;
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error updating UI visibility: {ex.Message}");
                
                // in case of error, disable all loading indicators
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    MyAudioLoadingRing.IsActive = false;
                    PopularLoadingRing.IsActive = false;
                    SearchLoadingRing.IsActive = false;
                    LoadingProgressRing.IsActive = false;
                });
            }
        }

        private async Task SearchAudio(string query)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    ShowError("Enter text to search");
                    return;
                }
                
                LoadingProgressRing.IsActive = true;
                
                ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // clear existing collection before new search
                    _recommendedAudioCollection.Clear();
                });
                
                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    ShowError("Unable to get access token");
                    LoadingProgressRing.IsActive = false;
                    SearchLoadingRing.IsActive = false;
                    return;
                }
                
                var httpClient = await SessionHelper.GetHttpClientAsync();
                // URL-encode search query
                string encodedQuery = Uri.EscapeDataString(query);
                string apiUrl = $"method/audio.search?access_token={token}&v=5.126&q={encodedQuery}&count=30";
                
                Debug.WriteLine($"[MusicPage] Searching audio with query: {query}");
                
                var response = await httpClient.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ShowError($"Search error: {response.StatusCode}");
                    LoadingProgressRing.IsActive = false;
                    SearchLoadingRing.IsActive = false;
                    return;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                
                // process
                try
                {
                    JObject jsonObject = JObject.Parse(content);
                    JObject responseObj = jsonObject["response"] as JObject;
                    
                    if (responseObj == null)
                    {
                        ShowError("Invalid API response");
                        LoadingProgressRing.IsActive = false;
                        SearchLoadingRing.IsActive = false;
                        return;
                    }
                    
                    int count = responseObj["count"]?.Value<int>() ?? 0;
                    JArray items = responseObj["items"] as JArray;
                    
                    if (items == null || items.Count == 0)
                    {
                        LoadingProgressRing.IsActive = false;
                        SearchLoadingRing.IsActive = false;
                        // update header with information about results
                        ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                        {
                            var pivotItem = AudioPivot.Items[2] as PivotItem;
                            if (pivotItem != null)
                            {
                                var grid = pivotItem.Content as Grid;
                                var headerTextBlock = grid?.Children.OfType<TextBlock>().FirstOrDefault();
                                if (headerTextBlock != null)
                                {
                                    headerTextBlock.Text = $"Search: {query} (no results)";
                    }
                }
            });
                        return;
                    }
                    
                    List<Models.Audio> searchResults = new List<Models.Audio>();
                    
                    foreach (JToken item in items)
                    {
                        // create audio object from JSON
                        Models.Audio audio = CreateAudioFromJToken(item);
                        if (audio != null)
                        {
                            searchResults.Add(audio);
                        }
                    }
                    
                    // add search results to recommendations collection (use it for display)
                    ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        foreach (var audio in searchResults)
                        {
                            _recommendedAudioCollection.Add(audio);
                        }
                        
                        // update header with information about results
                        var pivotItem = AudioPivot.Items[2] as PivotItem;
                        if (pivotItem != null)
                        {
                            var grid = pivotItem.Content as Grid;
                            var headerTextBlock = grid?.Children.OfType<TextBlock>().FirstOrDefault();
                            if (headerTextBlock != null)
                            {
                                headerTextBlock.Text = $"Search: {query} (found: {searchResults.Count})";
                            }
                        }
                        
                        UpdateUIVisibility(searchResults.Count > 0);
                    });
                    
                    Debug.WriteLine($"[MusicPage] Search completed, found {searchResults.Count} tracks");
                }
                catch (Exception ex)
                {
                    ShowError($"Error processing search results: {ex.Message}");
                    Debug.WriteLine($"[MusicPage] Error processing search results: {ex.Message}");
                }
                finally
                {
                    LoadingProgressRing.IsActive = false;
                    SearchLoadingRing.IsActive = false;
                }
            }
            catch (Exception ex)
            {
                LoadingProgressRing.IsActive = false;
                SearchLoadingRing.IsActive = false;
                ShowError($"Search error: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Error in SearchAudio: {ex.Message}");
            }
        }

        private async Task LoadRecommendedAudio()
        {
            try
            {
                // activate loading indicator for this section
                SearchLoadingRing.IsActive = true;
                
                // clear collection before adding new items
                _recommendedAudioCollection.Clear();
                
                // get access token
                string token = await SessionHelper.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    ShowError("Unable to get access token");
                    SearchLoadingRing.IsActive = false;
                    return;
                }
                
                var httpClient = await SessionHelper.GetHttpClientAsync();
                string apiUrl = $"method/audio.getRecommendations?access_token={token}&v=5.126&count=30";
                
                Debug.WriteLine("[MusicPage] Loading recommended audio");
                
                var response = await httpClient.GetAsync(apiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ShowError($"Error loading recommendations: {response.StatusCode}");
                    SearchLoadingRing.IsActive = false;
                    return;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                
                // process response
                try
                {
                    JObject jsonObject = JObject.Parse(content);
                    JObject responseObj = jsonObject["response"] as JObject;
                    
                    if (responseObj == null)
                    {
                        ShowError("Invalid API response");
                        SearchLoadingRing.IsActive = false;
                        return;
                    }
                    
                    JArray items = responseObj["items"] as JArray;
                    
                    if (items == null || items.Count == 0)
                    {
                        Debug.WriteLine("[MusicPage] No recommended audio found");
                        SearchLoadingRing.IsActive = false;
                        return;
                    }
                    
                    List<Models.Audio> recommendedAudios = new List<Models.Audio>();
                    
                    foreach (JToken item in items)
                    {
                        // create audio object from JSON
                        Models.Audio audio = CreateAudioFromJToken(item);
                        if (audio != null)
                        {
                            recommendedAudios.Add(audio);
                        }
                    }
                    
                    // add all elements to collection
                    foreach (var audio in recommendedAudios)
                    {
                        _recommendedAudioCollection.Add(audio);
                    }
                    
                    Debug.WriteLine($"[MusicPage] Loaded {_recommendedAudioCollection.Count} recommended audio tracks");
                    
                 // update visibility of interface elements
                    UpdateUIVisibility(recommendedAudios.Count > 0);
                }
                catch (Exception ex)
                {
                    ShowError($"Data processing error: {ex.Message}");
                    Debug.WriteLine($"[MusicPage] Error processing recommended audio data: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error loading recommendations: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Error in LoadRecommendedAudio: {ex.Message}");
            }
            finally
            {
                // deactivate loading indicator
                SearchLoadingRing.IsActive = false;
            }
        }

        private void PlayAudio(object sender, ItemClickEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[MusicPage] PlayAudio called with item type: {e.ClickedItem?.GetType().FullName ?? "null"}");
                
                if (e.ClickedItem == null)
                {
                    Debug.WriteLine("[MusicPage] ERROR: ClickedItem is null");
                    return;
                }
                
                // safe cast using our method
                Models.Audio audio = Models.Audio.SafeCast(e.ClickedItem);
                
                if (audio == null)
                {
                    Debug.WriteLine("[MusicPage] ERROR: Failed to cast item to Audio");
                    return;
                }
                
                Debug.WriteLine($"[MusicPage] Playing audio: {audio.Artist} - {audio.Title} (ID: {audio.Id})");
                
                // determine from which collection the record is selected and find the index
                int selectedIndex = -1;
                ObservableCollection<Models.Audio> currentCollection = null;
                
                if (_currentMode == AudioMode.MyAudio)
                {
                    // create a copy of the collection to avoid problems with removing elements
                    currentCollection = new ObservableCollection<Models.Audio>(_myAudioCollection);
                    // find the index of audio in the collection
                    selectedIndex = FindAudioIndexInCollection(audio, currentCollection);
                }
                else if (_currentMode == AudioMode.Popular)
                {
                    currentCollection = _popularAudioCollection;
                    // find the index of audio in the collection
                    selectedIndex = FindAudioIndexInCollection(audio, _popularAudioCollection);
                }
                else if (_currentMode == AudioMode.Search)
                {
                    currentCollection = _recommendedAudioCollection;
                    // find the index of audio in the collection
                    selectedIndex = FindAudioIndexInCollection(audio, _recommendedAudioCollection);
                    }
                    else
                    {
                    // by default, use popular audio
                    currentCollection = _popularAudioCollection;
                    selectedIndex = FindAudioIndexInCollection(audio, _popularAudioCollection);
                    }
                    
                // check for correct index
                if (selectedIndex >= 0 && currentCollection != null && currentCollection.Count > 0)
                    {
                    Debug.WriteLine($"[MusicPage] Setting playlist with index {selectedIndex}, collection count: {currentCollection.Count}");
                    _audioService.SetPlaylist(currentCollection, selectedIndex);
                    }
                    else
                    {
                    // if not found in collection, play only one track
                    Debug.WriteLine("[MusicPage] Audio not found in collection or collection is empty, playing single track");
                    var singleTrackList = new ObservableCollection<Models.Audio> { audio };
                    _audioService.SetPlaylist(singleTrackList, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in PlayAudio: {ex.Message}");
            }
        }
        
            // helper method for finding the
        private int FindAudioIndexInCollection(Models.Audio audio, ObservableCollection<Models.Audio> collection)
        {
            if (audio == null || collection == null || collection.Count == 0)
                return -1;
                
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i] != null && 
                    collection[i].Id == audio.Id && 
                    collection[i].OwnerId == audio.OwnerId)
                {
                    Debug.WriteLine($"[MusicPage] Found audio at index {i} in collection");
                    return i;
                }
            }
            
            Debug.WriteLine("[MusicPage] Audio not found in collection");
            return -1;
        }

        private void HighlightCurrentAudio(Models.Audio audio)
        {
            try
            {
                if (audio == null)
                {
                    Debug.WriteLine("[MusicPage] Cannot highlight null audio");
                    return;
                }
                
                Debug.WriteLine($"[MusicPage] Highlighting audio: {audio.Id}");
                
                // check if there is a corresponding record in the tables and highlight it
                bool found = false;
                
                // check in MyAudio
                for (int i = 0; i < _myAudioCollection.Count; i++)
        {
            try
            {
                        if (_myAudioCollection[i].Id == audio.Id && _myAudioCollection[i].OwnerId == audio.OwnerId)
                        {
                            MyAudioListView.SelectedIndex = i;
                            found = true;
                            Debug.WriteLine($"[MusicPage] Found in MyAudio at index {i}");
                            break;
                        }
                    }
                    catch (Exception itemEx)
                    {
                        Debug.WriteLine($"[MusicPage] Error comparing item in MyAudio: {itemEx.Message}");
                    }
                }
                
                // check in Popular
                if (!found)
                {
                    for (int i = 0; i < _popularAudioCollection.Count; i++)
        {
            try
            {
                            if (_popularAudioCollection[i].Id == audio.Id && _popularAudioCollection[i].OwnerId == audio.OwnerId)
                            {
                                PopularAudioListView.SelectedIndex = i;
                                found = true;
                                Debug.WriteLine($"[MusicPage] Found in Popular at index {i}");
                                break;
                            }
                        }
                        catch (Exception itemEx)
                        {
                            Debug.WriteLine($"[MusicPage] Error comparing item in Popular: {itemEx.Message}");
                        }
                    }
                }
                
                // check in Search/Recommended
                if (!found)
                {
                    for (int i = 0; i < _recommendedAudioCollection.Count; i++)
                    {
                        try
                        {
                            if (_recommendedAudioCollection[i].Id == audio.Id && _recommendedAudioCollection[i].OwnerId == audio.OwnerId)
                            {
                                RecommendedAudioListView.SelectedIndex = i;
                                Debug.WriteLine($"[MusicPage] Found in Recommended at index {i}");
                                break;
                            }
                        }
                        catch (Exception itemEx)
                        {
                            Debug.WriteLine($"[MusicPage] Error comparing item in Recommended: {itemEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in HighlightCurrentAudio: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[MusicPage] Stack trace: {ex.StackTrace}");
            }
        }

        // handler of favorite status change
        private async void AudioService_FavoriteStatusChanged(object sender, Models.Audio audio)
        {
            try
            {
                Debug.WriteLine($"[MusicPage] FavoriteStatusChanged event received for track {audio.Artist} - {audio.Title}, IsAdded={audio.IsAdded}");
                
                // if current mode is "My Audio", then update the list
                if (_currentMode == AudioMode.MyAudio)
                {
                    // if track was removed from favorites, remove it from collection
                    if (!audio.IsAdded)
                    {
                        var trackToRemove = _myAudioCollection.FirstOrDefault(a => a.Id == audio.Id && a.OwnerId == audio.OwnerId);
                        if (trackToRemove != null)
                        {
                            await ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                            {
                                _myAudioCollection.Remove(trackToRemove);
                                
                                // update header with the number of tracks
                                if (_myAudioCollection.Count > 0)
                                {
                                    MyAudioHeader.Text = $"My audios ({_myAudioCollection.Count})";
                                }
                                else
                                {
                                    MyAudioHeader.Text = "You have no saved audios";
                                    UpdateUIVisibility(false);
                                }
                                
                                Debug.WriteLine($"[MusicPage] Removed track from My Audio collection, new count: {_myAudioCollection.Count}");
                            });
                        }
                    }
                    // if track was added to favorites, add it to collection (if it is not there)
                    else if (audio.IsAdded && !_myAudioCollection.Any(a => a.Id == audio.Id && a.OwnerId == audio.OwnerId))
                    {
                        await ((Microsoft.UI.Dispatching.DispatcherQueue)DispatcherQueue).TryEnqueueAsync(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                        {
                            _myAudioCollection.Add(audio);
                            
                            // update header with the number of tracks
                            MyAudioHeader.Text = $"My audios ({_myAudioCollection.Count})";
                            
                            // if this is the first track, update UI
                            if (_myAudioCollection.Count == 1)
                            {
                                UpdateUIVisibility(true);
                            }
                            
                            Debug.WriteLine($"[MusicPage] Added track to My Audio collection, new count: {_myAudioCollection.Count}");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicPage] Error in AudioService_FavoriteStatusChanged: {ex.Message}");
            }
        }

        private void MusicPage_Unloaded(object sender, RoutedEventArgs e)
        {
            // unsubscribe from any events
            if (_audioService != null)
            {
                _audioService.FavoriteStatusChanged -= AudioService_FavoriteStatusChanged;
            }
            
            Debug.WriteLine("[MusicPage] Unloaded and unsubscribed from events");
        }

        //handler of "Search" button click
        private void SearchButtonInTab_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchBoxInTab.Text))
            {
                SearchLoadingRing.IsActive = true;
                _searchQuery = SearchBoxInTab.Text.Trim();
                _ = SearchAudio(_searchQuery);
            }
        }
        
        // handler of "Enter" in search field
        private void SearchBoxInTab_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(SearchBoxInTab.Text))
            {
                SearchLoadingRing.IsActive = true;
                _searchQuery = SearchBoxInTab.Text.Trim();
                _ = SearchAudio(_searchQuery);
            }
        }
    }
}
