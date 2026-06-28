using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ovkdesktop.Models;
using System.Diagnostics;
using Windows.Media.Core;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace ovkdesktop.Controls
{
    public sealed partial class PostMediaControl : UserControl
    {
        public static readonly DependencyProperty AttachmentsProperty =
            DependencyProperty.Register(
                nameof(Attachments),
                typeof(List<Attachment>),
                typeof(PostMediaControl),
                new PropertyMetadata(null, OnAttachmentsChanged));

        public List<Attachment> Attachments
        {
            get => (List<Attachment>)GetValue(AttachmentsProperty);
            set => SetValue(AttachmentsProperty, value);
        }

        public PostMediaControl()
        {
            this.InitializeComponent();
        }

        private static void OnAttachmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PostMediaControl control)
            {
                control.RenderAttachments(e.NewValue as List<Attachment>);
            }
        }

        private void RenderAttachments(List<Attachment> attachments)
        {
            MediaContainer.Children.Clear();

            if (attachments == null) return;

            foreach (var attachment in attachments)
            {
                if (attachment.Type == "photo" && attachment.Photo != null && !string.IsNullOrEmpty(attachment.Photo.LargestPhotoUrl))
                {
                    try
                    {
                        var bitmapImage = new BitmapImage(new Uri(attachment.Photo.LargestPhotoUrl));
                        // Optimize memory consumption by rendering smaller images initially
                        bitmapImage.DecodePixelWidth = 800;

                        var image = new Image
                        {
                            Source = bitmapImage,
                            Stretch = Stretch.Uniform,
                            MaxHeight = 400,
                            Margin = new Thickness(0, 5, 0, 5)
                        };

                        image.Tapped += (s, ev) =>
                        {
                            ImageViewerDialog.Show(attachment.Photo.LargestPhotoUrl, image.XamlRoot);
                        };
                        MediaContainer.Children.Add(image);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PostMediaControl] Image load error: {ex.Message}");
                    }
                }
                else if (attachment.Type == "video" && attachment.Video != null && !string.IsNullOrEmpty(attachment.Video.SafePlayerUrl))
                {
                    var videoHost = CreateVideoElement(attachment.Video);
                    MediaContainer.Children.Add(videoHost);
                }
            }
        }

        private FrameworkElement CreateVideoElement(Video video)
        {
            var videoHost = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            var previewGrid = new Grid { MaxHeight = 200, MaxWidth = 400, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left };

            if (!string.IsNullOrEmpty(video.LargestImageUrl))
            {
                try
                {
                    previewGrid.Children.Add(new Image { Source = new BitmapImage(new Uri(video.LargestImageUrl)), Stretch = Stretch.UniformToFill });
                }
                catch (Exception ex) { Debug.WriteLine($"[PostMediaControl] Video thumbnail error: {ex.Message}"); }
            }

            var playButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(170, 0, 0, 0)),
                Content = new FontIcon { Glyph = "\uE768", Foreground = new SolidColorBrush(Colors.White) }
            };
            previewGrid.Children.Add(playButton);
            videoHost.Children.Add(previewGrid);

            playButton.Click += (s, e) =>
            {
                previewGrid.Visibility = Visibility.Collapsed;
                var mediaPlayerElement = new MediaPlayerElement
                {
                    AreTransportControlsEnabled = true,
                    AutoPlay = true,
                    Source = MediaSource.CreateFromUri(new Uri(video.SafePlayerUrl)),
                    MaxHeight = 200,
                    MaxWidth = 400
                };
                videoHost.Children.Add(mediaPlayerElement);
            };

            return videoHost;
        }
    }
}
