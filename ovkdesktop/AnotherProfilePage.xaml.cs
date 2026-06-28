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
    public sealed partial class AnotherProfilePage : Page
    {
        public AnotherProfileViewModel ViewModel { get; }

        public AnotherProfilePage()
        {
            ViewModel = Ioc.Default.GetRequiredService<AnotherProfileViewModel>();
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is IConvertible convertible)
            {
                long ownerId = convertible.ToInt64(null);
                await ViewModel.InitializeAsync(ownerId);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.Cancel();
        }

        private void BackPostsClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void Author_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is IConvertible convertible)
            {
                long fromId = convertible.ToInt64(null);
                if (fromId == ViewModel.ProfileId) return;
                Frame.Navigate(typeof(AnotherProfilePage), fromId);
            }
        }

        private void RepostAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is IConvertible convertible)
            {
                long fromId = convertible.ToInt64(null);
                if (fromId == ViewModel.ProfileId) return;
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
            if (sender is FrameworkElement element && element.DataContext is Audio audio)
            {
                ViewModel.PlayAudioCommand.Execute(audio);
            }
        }

        private void LikeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is UserWallPost post)
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

        private void PageScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 500)
                {
                    if (ViewModel.CanLoadMore && !ViewModel.IsLoadingMore && !ViewModel.IsLoading)
                    {
                        ViewModel.LoadMorePostsCommand.Execute(null);
                    }
                }
            }
        }
    }
}
