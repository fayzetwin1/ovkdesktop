using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using ovkdesktop.Models;
using ovkdesktop.ViewModels;
using System.Diagnostics;
using ovkdesktop.Helpers;

namespace ovkdesktop
{
    public sealed partial class PostsPage : Page, Services.Interfaces.IRefreshablePage
    {
        public PostsViewModel ViewModel { get; }

        public PostsPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            ViewModel = CommunityToolkit.Mvvm.DependencyInjection.Ioc.Default.GetRequiredService<PostsViewModel>();
            this.DataContext = ViewModel;
        }

        public System.Threading.Tasks.Task RefreshAsync()
        {
            ViewModel.RefreshCommand.Execute(null);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private readonly ScrollPositionKeeper _scrollPosition = new();
        private bool _restoreScrollPosition;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _restoreScrollPosition = _scrollPosition.SavedOffset > 0;
            RestoreScrollPosition();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (_scrollViewer != null)
                _scrollPosition.Save(_scrollViewer);
            _scrollPosition.CancelPendingRestore();
            base.OnNavigatedFrom(e);
        }

        private void PostCard_AuthorInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            Frame?.Navigate(typeof(AnotherProfilePage), e.Post.FromId);
        }

        private void PostCard_CommentInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            Frame?.Navigate(typeof(PostInfoPage), new PostInfoPage.PostInfoParameters
            {
                PostId = e.Post.Id,
                OwnerId = e.Post.OwnerId
            });
        }

        private void PostCard_LikeInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            if (e.Post is NewsFeedPost post)
                ViewModel.LikePostCommand.Execute(post);
        }

        private async void PostCard_RepostInvoked(object sender, Controls.PostInvokedEventArgs e)
        {
            if (e.Post is not NewsFeedPost post)
                return;

            bool success = await ViewModel.RepostPostDirectlyAsync(post);
            var dialog = new ContentDialog
            {
                Title = success ? "Успех" : "Ошибка",
                Content = success ? "Запись успешно репостнута на вашу стену." : "Не удалось сделать репост.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.Themed();
            await dialog.ShowAsync();
        }

        private ScrollViewer _scrollViewer;

        private void PostsListView_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = FindVisualChild<ScrollViewer>(PostsListView);
            if (_scrollViewer != null)
            {
                _scrollViewer.ViewChanged -= ScrollViewer_ViewChanged;
                _scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
                RestoreScrollPosition();
            }
        }

        private void RestoreScrollPosition()
        {
            if (!_restoreScrollPosition || _scrollViewer == null)
                return;

            _restoreScrollPosition = false;
            _scrollPosition.Restore(_scrollViewer);
            Debug.WriteLine($"[PostsPage] Requested feed position restore: {_scrollPosition.SavedOffset:0}px");
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // Auto-load posts when the user is near the bottom
            if (_scrollViewer.VerticalOffset >= _scrollViewer.ScrollableHeight - 500)
            {
                if (ViewModel.CanLoadMore && !ViewModel.IsLoadingMore && !ViewModel.IsLoading)
                {
                    ViewModel.LoadMoreCommand.Execute(null);
                }
            }
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

        private void OnAvatarClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is int profileId)
            {
                if (this.Frame != null)
                {
                    this.Frame.Navigate(typeof(AnotherProfilePage), profileId);
                }
            }
        }

        private void OnCommentClicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[PostsPage] OnCommentClicked sender: {sender}");
            if (sender is Button button)
            {
                Debug.WriteLine($"[PostsPage] button.Tag is {button.Tag?.GetType()?.Name ?? "null"}");
                if (button.Tag is NewsFeedPost post)
                {
                    if (this.Frame != null)
                    {
                        var parameters = new PostInfoPage.PostInfoParameters
                        {
                            PostId = post.Id,
                            OwnerId = post.OwnerId
                        };
                        this.Frame.Navigate(typeof(PostInfoPage), parameters);
                    }
                    else
                    {
                        Debug.WriteLine("[PostsPage] this.Frame is null");
                    }
                }
            }
        }

        private void OnLikeClicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[PostsPage] OnLikeClicked sender: {sender}");
            if (sender is Button button && button.Tag is NewsFeedPost post)
            {
                ViewModel.LikePostCommand.Execute(post);
            }
        }

        private async void OnRepostClicked(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[PostsPage] OnRepostClicked sender: {sender}");
            if (sender is Button button && button.Tag is NewsFeedPost post)
            {
                bool success = await ViewModel.RepostPostDirectlyAsync(post);
                var dialog = new ContentDialog
                {
                    Title = success ? "Успех" : "Ошибка",
                    Content = success ? "Запись успешно репостнута на вашу стену." : "Не удалось сделать репост.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                }.Themed();
                await dialog.ShowAsync();
            }
        }
    }
}
