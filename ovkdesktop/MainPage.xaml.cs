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
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ApplicationSettings;
using Microsoft.UI.Xaml.Media.Animation;
using System.Diagnostics;
using ovkdesktop.Models;

namespace ovkdesktop
{
    public sealed partial class MainPage : Page
    {
        // service for managing playback
        private AudioPlayerService _audioPlayerService;
        
        // current page
        private Type _currentPageType;
        
        // mini player control
        private MiniPlayerControl _miniPlayerControl;
        
        public MainPage()
        {
            InitializeComponent();
            
            // get the audio player service from MainWindow
            var mainWindow = App.MainWindow as MainWindow;
            _audioPlayerService = mainWindow?.GetAudioPlayerService() ?? App.AudioService;
            
            // initialize the mini player
            InitializeMiniPlayer();
            
            // subscribe to the event of changing the current track
            _audioPlayerService.CurrentAudioChanged += AudioService_CurrentAudioChanged;
            
            // set the initial page
            ContentFrame.Navigate(typeof(PostsPage));
            _currentPageType = typeof(PostsPage);
            
            // subscribe to the event of navigation
            ContentFrame.Navigated += ContentFrame_Navigated;
            
            Debug.WriteLine("[MainPage] Initialized");
        }
        
        // initialization of the mini player
        private void InitializeMiniPlayer()
        {
            try
            {
                Debug.WriteLine("[MainPage] Initializing mini player");
                if (_audioPlayerService == null)
                {
                    _audioPlayerService = App.AudioService;
                if (_audioPlayerService == null)
                {
                    Debug.WriteLine("[MainPage] AudioPlayerService is null, cannot initialize mini player");
                    return;
                    }
                }
                
                // create the mini player control
                _miniPlayerControl = new MiniPlayerControl();
                
                // set the control as the content of the ContentControl
                MiniPlayerContainer.Content = _miniPlayerControl;
                
                // pass the audio service to the mini player for correct operation
                _miniPlayerControl.Initialize(_audioPlayerService);
                
                Debug.WriteLine("[MainPage] Mini player initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error initializing mini player: {ex.Message}");
            }
        }
        
        // handler of the event of changing the current track
        private void AudioService_CurrentAudioChanged(object sender, Models.Audio audio)
        {
            try
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                {
                    // show the mini player if there is a current track
                    if (audio != null)
                    {
                        // do not show the player on the welcome and authorization pages
                        if (_currentPageType != typeof(WelcomePage) && _currentPageType != typeof(AuthPage))
                        {
                            _miniPlayerControl?.Show();
                            Debug.WriteLine("[MainPage] Show mini player on audio change");
                        }
                    }
                    else
                    {
                        _miniPlayerControl?.Hide();
                        Debug.WriteLine("[MainPage] Hide mini player (null audio)");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error in AudioService_CurrentAudioChanged: {ex.Message}");
            }
        }
        
        // handler of the event of navigation
        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            try
            {
                // update the current page type
                _currentPageType = e.SourcePageType;
                
                // update the selected item in the NavigationView
                if (e.SourcePageType == typeof(PostsPage))
                {
                    NavView.SelectedItem = NewsItem;
                }
                else if (e.SourcePageType == typeof(ProfilePage))
                {
                    NavView.SelectedItem = ProfileItem;
                }
                else if (e.SourcePageType == typeof(MusicPage))
                {
                    NavView.SelectedItem = MusicItem;
                }
                else if (e.SourcePageType == typeof(FriendsPage))
                {
                    NavView.SelectedItem = FriendsItem;
                }
                else if (e.SourcePageType == typeof(SettingsClientPage))
                {
                    NavView.SelectedItem = SettingsItem;
                }
                
                Debug.WriteLine($"[MainPage] Navigated to {e.SourcePageType.Name}");
                
                // show or hide the mini player depending on the page
                UpdateMiniPlayerVisibility(e.SourcePageType);
                
                // after navigation, check if the mini player needs to be prepared for the next page
                PreparePlayerForNextNavigation(e.SourcePageType);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error in ContentFrame_Navigated: {ex.Message}");
            }
        }
        
        // prepares the mini player when navigating between pages
        private void PreparePlayerForNextNavigation(Type currentPageType)
        {
            try
            {
                // if we have navigated from a page with its own player (for example, MusicPage or AudioPage)
                // to a page where a mini player is needed, make sure the mini player is correctly initialized
                bool isAudioPageType = (currentPageType == typeof(MusicPage));
                
                if (!isAudioPageType && _audioPlayerService?.CurrentAudio != null)
                {
                    // make sure the mini player has up-to-date information about the current track
                    Debug.WriteLine("[MainPage] Ensuring mini player has current track info after navigation");
                    _miniPlayerControl?.Initialize(_audioPlayerService);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error in PreparePlayerForNextNavigation: {ex.Message}");
            }
        }
        
        // updating the visibility of the mini player depending on the current page
        private void UpdateMiniPlayerVisibility(Type pageType)
        {
            try
            {
                Debug.WriteLine($"[MainPage] UpdateMiniPlayerVisibility called for page type: {pageType.Name}");
                
                // hide the player on the welcome and authorization pages
                if (pageType == typeof(WelcomePage) || pageType == typeof(AuthPage))
                {
                    Debug.WriteLine($"[MainPage] Hiding mini player on {pageType.Name}");
                    _miniPlayerControl?.Hide();
                    return;
                }
                
                // on the MusicPage and AudioPage pages, the mini player is not needed, because there is its own player
                if (pageType == typeof(MusicPage))
                {
                    Debug.WriteLine($"[MainPage] Hiding mini player on {pageType.Name} - page has its own player");
                    _miniPlayerControl?.Hide();
                    return;
                }
                
                // show the player on other pages if there is a current track
                if (_audioPlayerService?.CurrentAudio != null)
                {
                    Debug.WriteLine($"[MainPage] Showing mini player on {pageType.Name} - current audio exists");
                    _miniPlayerControl?.Show();
                }
                else
                {
                    Debug.WriteLine($"[MainPage] Keeping mini player hidden on {pageType.Name} - no current audio");
                    _miniPlayerControl?.Hide();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error in UpdateMiniPlayerVisibility: {ex.Message}");
            }
        }
        
        // handler of the selection of an item in the NavigationView
        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {
                if (args.IsSettingsInvoked)
                {
                    if (_currentPageType != typeof(SettingsClientPage))
                    {
                        ContentFrame.Navigate(typeof(SettingsClientPage), null, new DrillInNavigationTransitionInfo());
                    }
                }
                else
                {
                    var navItemTag = args.InvokedItemContainer.Tag.ToString();
                    
                    Type pageType = navItemTag switch
                    {
                        "news" => typeof(PostsPage),
                        "profile" => typeof(ProfilePage),
                        "music" => typeof(MusicPage),
                        "friends" => typeof(FriendsPage),
                        "settings" => typeof(SettingsClientPage),
                        _ => null
                    };
                    
                    if (pageType != null && _currentPageType != pageType)
                    {
                        ContentFrame.Navigate(pageType, null, new DrillInNavigationTransitionInfo());
                    }
                    
                    Debug.WriteLine($"[MainPage] Navigation item invoked: {navItemTag}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error in NavView_ItemInvoked: {ex.Message}");
            }
        }
        
        // handler of the click on the exit button
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // clear the token and go to the welcome page
                SessionHelper.ClearToken();
                
                // hide the mini player
                _miniPlayerControl?.Hide();
                
                // go to the welcome page
                Frame.Navigate(typeof(WelcomePage));
                
                Debug.WriteLine("[MainPage] Logged out");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Error in LogoutButton_Click: {ex.Message}");
            }
        }
    }
}
