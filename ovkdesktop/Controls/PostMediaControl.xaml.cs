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
using System.Linq;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.UI.Xaml.Documents;

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

            var audioPlaylist = attachments
                .Where(attachment => attachment.Type == "audio" && attachment.Audio != null)
                .Select(attachment => attachment.Audio)
                .ToList();

            foreach (var attachment in attachments)
            {
                if (attachment.Type == "photo" && attachment.Photo != null && !string.IsNullOrEmpty(attachment.Photo.TimelinePhotoUrl))
                {
                    try
                    {
                        var bitmapImage = new BitmapImage(new Uri(attachment.Photo.TimelinePhotoUrl));
                        // Optimize memory consumption by rendering smaller images initially
                        bitmapImage.DecodePixelWidth = 800;

                        var image = new Image
                        {
                            Source = bitmapImage,
                            Stretch = Stretch.Uniform,
                            MaxHeight = 400,
                            MaxWidth = 900,
                            HorizontalAlignment = HorizontalAlignment.Center,
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
                else if (attachment.Type == "audio" && attachment.Audio != null)
                {
                    MediaContainer.Children.Add(CreateAudioElement(attachment.Audio, audioPlaylist));
                }
            }
        }

        private FrameworkElement CreateAudioElement(Audio audio, List<Audio> playlist)
        {
            var playButton = new Button
            {
                Width = 38,
                Height = 38,
                MinWidth = 38,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(19),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 91, 157, 229)),
                Content = new FontIcon
                {
                    Glyph = "\uE768",
                    FontSize = 15,
                    Foreground = new SolidColorBrush(Colors.White)
                },
                IsEnabled = !string.IsNullOrWhiteSpace(audio.Url),
                VerticalAlignment = VerticalAlignment.Center
            };

            ToolTipService.SetToolTip(playButton,
                playButton.IsEnabled ? $"Воспроизвести {audio.FullTitle}" : "Аудиозапись недоступна");

            string artist = string.IsNullOrWhiteSpace(audio.Artist)
                ? "Неизвестный исполнитель"
                : audio.Artist;
            string title = string.IsNullOrWhiteSpace(audio.Title)
                ? "Без названия"
                : audio.Title;

            var trackText = new TextBlock
            {
                Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            trackText.Inlines.Add(new Run
            {
                Text = artist,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 91, 157, 229))
            });
            trackText.Inlines.Add(new Run
            {
                Text = $" — {title}"
            });

            var duration = new TextBlock
            {
                Text = audio.FormattedDuration,
                Opacity = 0.68,
                VerticalAlignment = VerticalAlignment.Center
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(trackText, 1);
            Grid.SetColumn(duration, 2);
            grid.Children.Add(playButton);
            grid.Children.Add(trackText);
            grid.Children.Add(duration);

            playButton.Click += (_, _) =>
            {
                try
                {
                    var service = Ioc.Default.GetRequiredService<AudioPlayerService>();
                    int index = Math.Max(0, playlist.IndexOf(audio));
                    service.SetPlaylist(playlist, index);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PostMediaControl] Audio playback error: {ex.Message}");
                }
            };

            return new Border
            {
                Background = Application.Current.Resources["ClientNestedSurfaceBrush"] as Brush,
                BorderBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 4, 0, 4),
                Child = grid
            };
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

            playButton.Click += async (s, e) =>
            {
                previewGrid.Visibility = Visibility.Collapsed;
                string playerUrl = System.Net.WebUtility.HtmlDecode(video.SafePlayerUrl);

                bool isWebPlayer = playerUrl.Contains("video_ext.php") || 
                                   playerUrl.Contains("youtube.com") || 
                                   playerUrl.Contains("youtu.be") ||
                                   playerUrl.Contains("rutube.ru") ||
                                   playerUrl.Contains("vimeo.com");

                if (!isWebPlayer)
                {
                    var mediaPlayerElement = new MediaPlayerElement
                    {
                        AreTransportControlsEnabled = true,
                        AutoPlay = true,
                        Source = MediaSource.CreateFromUri(new Uri(playerUrl)),
                        MaxHeight = 300,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    videoHost.Children.Add(mediaPlayerElement);
                }
                else
                {
                    try 
                    {
                        var webView = new WebView2
                        {
                            Source = new Uri(playerUrl),
                            Height = 300,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        await webView.EnsureCoreWebView2Async();
                        videoHost.Children.Add(webView);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PostMediaControl] WebView2 initialization failed: {ex}");
                        await Windows.System.Launcher.LaunchUriAsync(new Uri(playerUrl));
                    }
                }
            };

            return videoHost;
        }
    }
}
