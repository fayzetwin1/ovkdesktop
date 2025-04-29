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



namespace ovkdesktop
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            NavigationMainPage.SelectedItem = NavigationMainPage.MenuItems[0];
        }

        private void NavigationViewControl_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var selectedItem = args.SelectedItem as NavigationViewItem;
            if (selectedItem == null) return;

            var tag = selectedItem.Tag as string;

            switch (tag)
            {
                case "ProfilePage":
                    ContentMainFrame.Navigate(typeof(ProfilePage));
                    break;
                case "FriendsPage":
                    ContentMainFrame.Navigate(typeof(FriendsPage));
                    break;
                case "PostsPage":
                    ContentMainFrame.Navigate(typeof(PostsPage));
                    break;
                case "MusicPage":
                    ContentMainFrame.Navigate(typeof(MusicPage));
                    break;
                case "SettingsClientPage":
                    ContentMainFrame.Navigate(typeof(SettingsClientPage));
                    break;
            }
        }

        private void NavigationViewControl_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked) 
            {
                ContentMainFrame.Navigate(typeof(SettingsPage));
            }
        }
    }
}
