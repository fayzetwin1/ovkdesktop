using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.ViewModels;
using ovkdesktop.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using ovkdesktop.Helpers;

namespace ovkdesktop
{
    public sealed partial class ProfilePage : Page
    {
        public ProfileViewModel ViewModel { get; }

        public ProfilePage()
        {
            ViewModel = Ioc.Default.GetRequiredService<ProfileViewModel>();
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private readonly ScrollPositionKeeper _scrollPosition = new();
        private bool _restoreScrollPosition;

        private void PostCard_AuthorInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            if (ViewModel.CurrentUserProfile != null && e.Post.FromId == ViewModel.CurrentUserProfile.Id)
                return;
            Frame.Navigate(typeof(AnotherProfilePage), e.Post.FromId);
        }

        private void PostCard_CommentInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            Frame.Navigate(typeof(PostInfoPage), new PostInfoPage.PostInfoParameters
            {
                PostId = e.Post.Id,
                OwnerId = e.Post.OwnerId
            });
        }

        private void PostCard_LikeInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            if (e.Post is UserWallPost post)
                ViewModel.ToggleLikeCommand.Execute(post);
        }

        private void PostCard_RepostInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            if (e.Post is UserWallPost post)
                ViewModel.RepostCommand.Execute(post);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _restoreScrollPosition = _scrollPosition.SavedOffset > 0;
            if (ViewModel.Posts.Count == 0)
                ViewModel.InitializeCommand.Execute(null);
            RestoreScrollPosition();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _scrollPosition.Save(_scrollViewer);
            _scrollPosition.CancelPendingRestore();
            base.OnNavigatedFrom(e);
            ViewModel.Cancel();
        }

        private void PublishNewPostButton(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(TypeNewPostPage));
        }

        private ScrollViewer _scrollViewer;

        private void PostsListView_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = FindVisualChild<ScrollViewer>(PostsListView);
            if (_scrollViewer != null)
            {
                _scrollViewer.ViewChanged -= PageScrollViewer_ViewChanged;
                _scrollViewer.ViewChanged += PageScrollViewer_ViewChanged;
                RestoreScrollPosition();
            }
        }

        private void RestoreScrollPosition()
        {
            if (!_restoreScrollPosition || _scrollViewer == null)
                return;

            _restoreScrollPosition = false;
            _scrollPosition.Restore(_scrollViewer);
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return default;
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

        private void Author_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is IConvertible convertible)
            {
                long fromId = convertible.ToInt64(null);
                if (ViewModel.CurrentUserProfile != null && fromId == ViewModel.CurrentUserProfile.Id) return;
                Frame.Navigate(typeof(AnotherProfilePage), fromId);
            }
        }

        private void RepostAuthor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is IConvertible convertible)
            {
                long fromId = convertible.ToInt64(null);
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
