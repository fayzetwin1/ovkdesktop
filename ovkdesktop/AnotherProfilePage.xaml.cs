using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.ViewModels;
using ovkdesktop.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using ovkdesktop.Helpers;
using System.Collections.Generic;

namespace ovkdesktop
{
    public sealed partial class AnotherProfilePage : Page
    {
        public AnotherProfileViewModel ViewModel { get; }

        public AnotherProfilePage()
        {
            ViewModel = Ioc.Default.GetRequiredService<AnotherProfileViewModel>();
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private static readonly Dictionary<long, double> SavedOffsets = new();
        private readonly ScrollPositionKeeper _scrollPosition = new();
        private long _requestedProfileId;

        // the ListView owns the scrolling now, so its inner ScrollViewer is what the
        // infinite-scroll trigger and the saved position have to work against
        private ScrollViewer _scrollViewer;

        private void PostsListView_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = FindVisualChild<ScrollViewer>(PostsListView);
            if (_scrollViewer == null)
                return;

            _scrollViewer.ViewChanged -= PageScrollViewer_ViewChanged;
            _scrollViewer.ViewChanged += PageScrollViewer_ViewChanged;

            if (_requestedProfileId != 0 && SavedOffsets.TryGetValue(_requestedProfileId, out var offset))
                _scrollPosition.Restore(_scrollViewer, offset);
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return default;
        }

        private void PostCard_AuthorInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            if (e.Post.FromId == ViewModel.ProfileId)
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

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is IConvertible convertible)
            {
                long ownerId = convertible.ToInt64(null);
                _requestedProfileId = ownerId;
                if (ViewModel.ProfileId != ownerId || ViewModel.Posts.Count == 0)
                    await ViewModel.InitializeAsync(ownerId);

                if (_scrollViewer != null && SavedOffsets.TryGetValue(ownerId, out var offset))
                    DispatcherQueue.TryEnqueue(() => _scrollPosition.Restore(_scrollViewer, offset));
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (_requestedProfileId != 0 && _scrollViewer != null)
                SavedOffsets[_requestedProfileId] = _scrollViewer.VerticalOffset;
            _scrollPosition.CancelPendingRestore();
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
