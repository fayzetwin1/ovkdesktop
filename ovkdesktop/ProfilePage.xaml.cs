using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.ViewModels;
using ovkdesktop.Models;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace ovkdesktop
{
    public sealed partial class ProfilePage : Page
    {
        public ProfileViewModel ViewModel { get; }

        public ProfilePage()
        {
            ViewModel = Ioc.Default.GetRequiredService<ProfileViewModel>();
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.InitializeCommand.Execute(null);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.Cancel();
        }

        private void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(TypeNewPostPage));
        }

        private void Author_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is long fromId)
            {
                if (ViewModel.CurrentUserProfile != null && fromId == ViewModel.CurrentUserProfile.Id) return;
                Frame.Navigate(typeof(AnotherProfilePage), fromId);
            }
        }

        private void RepostAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is long fromId)
            {
                if (ViewModel.CurrentUserProfile != null && fromId == ViewModel.CurrentUserProfile.Id) return;
                Frame.Navigate(typeof(AnotherProfilePage), fromId);
            }
        }

        private void ShowPostComments_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is UserWallPost post)
            {
                var ownerId = post.OwnerId;
                if (post.CopyHistory != null && post.CopyHistory.Count > 0)
                {
                    ownerId = post.CopyHistory[0].OwnerId;
                }
                Frame.Navigate(typeof(PostInfoPage), new { Post = post, OwnerId = ownerId });
            }
        }

        private void PlayAudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Audio audio)
            {
                ViewModel.PlayAudioCommand.Execute(audio);
            }
        }

        private void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is UserWallPost post)
            {
                ViewModel.ToggleLikeCommand.Execute(post);
            }
        }

        private void RepostItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is UserWallPost post)
            {
                ViewModel.RepostCommand.Execute(post);
            }
        }
    }
}
