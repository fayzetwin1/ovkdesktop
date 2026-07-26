using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ovkdesktop.Models;

namespace ovkdesktop.Controls
{
    public sealed class PostInvokedEventArgs : EventArgs
    {
        public PostInvokedEventArgs(BasePost post)
        {
            Post = post;
        }

        public BasePost Post { get; }
    }

    public sealed partial class PostCardControl : UserControl
    {
        public static readonly DependencyProperty PostProperty = DependencyProperty.Register(
            nameof(Post),
            typeof(BasePost),
            typeof(PostCardControl),
            new PropertyMetadata(null));

        public static readonly DependencyProperty ShowActionsProperty = DependencyProperty.Register(
            nameof(ShowActions),
            typeof(bool),
            typeof(PostCardControl),
            new PropertyMetadata(true));

        public static readonly DependencyProperty IsNestedProperty = DependencyProperty.Register(
            nameof(IsNested),
            typeof(bool),
            typeof(PostCardControl),
            new PropertyMetadata(false, OnIsNestedChanged));

        public BasePost Post
        {
            get => (BasePost)GetValue(PostProperty);
            set => SetValue(PostProperty, value);
        }

        public bool ShowActions
        {
            get => (bool)GetValue(ShowActionsProperty);
            set => SetValue(ShowActionsProperty, value);
        }

        public bool IsNested
        {
            get => (bool)GetValue(IsNestedProperty);
            set => SetValue(IsNestedProperty, value);
        }

        public event EventHandler<PostInvokedEventArgs> AuthorInvoked;
        public event EventHandler<PostInvokedEventArgs> LikeInvoked;
        public event EventHandler<PostInvokedEventArgs> CommentInvoked;
        public event EventHandler<PostInvokedEventArgs> RepostInvoked;

        public PostCardControl()
        {
            InitializeComponent();
        }

        private static void OnIsNestedChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            if (dependencyObject is not PostCardControl control || args.NewValue is not bool isNested)
                return;


            string styleKey = isNested ? "NestedSurfaceCardStyle" : "SurfaceCardStyle";
            if (Application.Current.Resources[styleKey] is Style cardStyle)
                control.CardBorder.Style = cardStyle;

            control.AuthorPicture.Width = control.AuthorPicture.Height = isNested ? 38 : 48;
        }

        private void AuthorButton_Click(object sender, RoutedEventArgs e) => Raise(AuthorInvoked);
        private void LikeButton_Click(object sender, RoutedEventArgs e) => Raise(LikeInvoked);
        private void CommentButton_Click(object sender, RoutedEventArgs e) => Raise(CommentInvoked);
        private void RepostButton_Click(object sender, RoutedEventArgs e) => Raise(RepostInvoked);

        private void NestedPost_AuthorInvoked(object sender, PostInvokedEventArgs e) => AuthorInvoked?.Invoke(this, e);
        private void NestedPost_CommentInvoked(object sender, PostInvokedEventArgs e) => CommentInvoked?.Invoke(this, e);

        private void Raise(EventHandler<PostInvokedEventArgs> handler)
        {
            if (Post != null)
                handler?.Invoke(this, new PostInvokedEventArgs(Post));
        }
    }
}
