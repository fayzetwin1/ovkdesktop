using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ovkdesktop.Models;
using ovkdesktop.Helpers;
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

            // an always-visible control with an empty container still consumes the
            // surrounding StackPanel.Spacing of the post card, which shows up as dead
            // vertical space on every text-only post
            Visibility = Visibility.Collapsed;

            if (attachments == null) return;

            var audioPlaylist = attachments
                .Where(attachment => attachment.Type == "audio" && attachment.Audio != null)
                .Select(attachment => attachment.Audio)
                .ToList();

            // photos are laid out together as one album block, everything else keeps
            // its own row underneath
            var photos = attachments
                .Where(a => a.Type == "photo" && a.Photo != null && !string.IsNullOrEmpty(a.Photo.TimelinePhotoUrl))
                .Select(a => a.Photo)
                .ToList();

            if (photos.Count > 0)
                MediaContainer.Children.Add(CreatePhotoAlbum(photos));

            foreach (var attachment in attachments)
            {
                if (attachment.Type == "photo")
                    continue; // already rendered as part of the album

                if (attachment.Type == "video" && attachment.Video != null && !string.IsNullOrEmpty(attachment.Video.SafePlayerUrl))
                {
                    MediaContainer.Children.Add(CreateVideoElement(attachment.Video));
                }
                else if (attachment.Type == "audio" && attachment.Audio != null)
                {
                    MediaContainer.Children.Add(CreateAudioElement(attachment.Audio, audioPlaylist));
                }
                else if (attachment.IsGif && !string.IsNullOrEmpty(attachment.Doc?.Url))
                {
                    // gifs arrive as documents; WinUI animates them through BitmapImage
                    try
                    {
                        var gif = new Image
                        {
                            Source = ImageUrlHelper.CreateBitmap(attachment.Doc.Url),
                            Stretch = Stretch.Uniform,
                            MaxWidth = MediaMaxWidth,
                            MaxHeight = MediaMaxHeight
                        };
                        MediaContainer.Children.Add(Rounded(gif));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PostMediaControl] GIF load error: {ex.Message}");
                    }
                }
                else
                {
                    // an attachment type the client cannot draw yet - say so instead of
                    // leaving a card that looks broken or empty
                    MediaContainer.Children.Add(CreateUnsupportedElement(attachment));
                }
            }

            if (MediaContainer.Children.Count > 0)
                Visibility = Visibility.Visible;
        }

        // a comfortable reading width; media wider than this dwarfs the post text
        private const double MediaMaxWidth = 680;
        private const double MediaMaxHeight = 460;
        private const double MediaCornerRadius = 10;
        private const double TileGap = 4;
        private const double TileHeight = 190;
        private const int MaxTiles = 6;

        /// <summary>wraps an element in the rounded, clipping frame all media shares</summary>
        private static Border Rounded(UIElement child, double radius = MediaCornerRadius) => new()
        {
            // WinUI Borders clip their child to the corner radius, so no extra clip geometry
            CornerRadius = new CornerRadius(radius),
            // the themed placeholder colour has to come from a Style: theme-dictionary
            // keys are not reachable through the Application.Resources indexer
            Style = Application.Current.Resources["MediaFrameStyle"] as Style,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = child
        };

        /// <summary>
        /// One photo keeps its aspect ratio; several are tiled into a two column grid,
        /// with an odd last one spanning the full width so the block stays rectangular.
        /// </summary>
        private FrameworkElement CreatePhotoAlbum(List<Photo> photos)
        {
            if (photos.Count == 1)
                return BuildSinglePhoto(photos[0]);

            int visible = Math.Min(photos.Count, MaxTiles);
            int hidden = photos.Count - visible;

            var grid = new Grid
            {
                ColumnSpacing = TileGap,
                RowSpacing = TileGap,
                MaxWidth = MediaMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int rows = (visible + 1) / 2;
            for (int i = 0; i < rows; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TileHeight) });

            for (int i = 0; i < visible; i++)
            {
                bool isLast = i == visible - 1;
                bool spansRow = isLast && visible % 2 == 1;

                var tile = BuildPhotoTile(photos[i], isLast && hidden > 0 ? hidden : 0);
                Grid.SetRow(tile, i / 2);
                Grid.SetColumn(tile, spansRow ? 0 : i % 2);
                if (spansRow)
                    Grid.SetColumnSpan(tile, 2);

                grid.Children.Add(tile);
            }

            return grid;
        }

        private FrameworkElement BuildSinglePhoto(Photo photo)
        {
            var image = CreatePhotoImage(photo, Stretch.Uniform);
            var size = photo.TimelineSize;

            if (size == null || size.Width <= 0 || size.Height <= 0)
            {
                image.MaxWidth = MediaMaxWidth;
                image.MaxHeight = MediaMaxHeight;
                return Rounded(image);
            }

            // A Grid at the photo's real pixel size gives the Viewbox something concrete to
            // measure, so the slot is the right shape before the bitmap arrives and the
            // feed stops reflowing. DownOnly keeps small photos at their own resolution.
            var holder = new Grid { Width = size.Width, Height = size.Height };
            holder.Children.Add(image);

            return Rounded(new Viewbox
            {
                Child = holder,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                MaxWidth = Math.Min(MediaMaxWidth, size.Width),
                MaxHeight = MediaMaxHeight
            });
        }

        /// <summary>a square-ish album cell; the image is cropped to fill it</summary>
        private FrameworkElement BuildPhotoTile(Photo photo, int remainingCount)
        {
            var cell = new Grid();
            cell.Children.Add(CreatePhotoImage(photo, Stretch.UniformToFill));

            if (remainingCount > 0)
            {
                cell.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(150, 0, 0, 0)),
                    Child = new TextBlock
                    {
                        Text = $"+{remainingCount}",
                        FontSize = 26,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });
            }

            var frame = Rounded(cell);
            frame.HorizontalAlignment = HorizontalAlignment.Stretch;
            return frame;
        }

        private Image CreatePhotoImage(Photo photo, Stretch stretch)
        {
            // 800px keeps decoding cheap; the album never shows more than ~680px wide
            var bitmap = ImageUrlHelper.CreateBitmap(photo.TimelinePhotoUrl, 800);

            var image = new Image { Source = bitmap, Stretch = stretch };

            image.ImageFailed += (s, ev) =>
                Debug.WriteLine($"[PostMediaControl] Image failed: {ev.ErrorMessage} ({photo.TimelinePhotoUrl})");

            image.Tapped += (s, ev) =>
                ImageViewerDialog.Show(ImageUrlHelper.ToSecure(photo.LargestPhotoUrl), image.XamlRoot);

            ToolTipService.SetToolTip(image, "Открыть изображение");
            return image;
        }

        /// <summary>
        /// A compact chip for attachments the client has no renderer for (polls, notes,
        /// documents, …). Without it such a post is indistinguishable from a broken one.
        /// </summary>
        private static FrameworkElement CreateUnsupportedElement(Attachment attachment)
        {
            (string glyph, string label) = attachment.Type switch
            {
                "poll" => ("", "Опрос"),
                "note" => ("", "Заметка"),
                "link" => ("", "Ссылка"),
                "graffiti" => ("", "Граффити"),
                "doc" => ("", string.IsNullOrWhiteSpace(attachment.Doc?.Title)
                    ? "Документ"
                    : $"Документ: {attachment.Doc.Title}"),
                _ => ("", $"Вложение «{attachment.Type}» пока не поддерживается")
            };

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            return new Border
            {
                Style = Application.Current.Resources["MediaAttachmentCardStyle"] as Style,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = row
            };
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

            // the style carries the themed background/border - see App.xaml
            return new Border
            {
                Style = Application.Current.Resources["MediaAttachmentCardStyle"] as Style,
                MaxWidth = MediaMaxWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = grid
            };
        }

        private static string FormatDuration(int seconds)
        {
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
                : $"{span.Minutes}:{span.Seconds:D2}";
        }

        private FrameworkElement CreateVideoElement(Video video)
        {
            var videoHost = new Grid { HorizontalAlignment = HorizontalAlignment.Left };

            // 16:9 plate so the slot is reserved before the thumbnail loads
            var previewGrid = new Grid
            {
                Width = MediaMaxWidth,
                Height = MediaMaxWidth * 9 / 16,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 24, 26, 32))
            };

            if (!string.IsNullOrEmpty(video.LargestImageUrl))
            {
                try
                {
                    previewGrid.Children.Add(new Image
                    {
                        Source = ImageUrlHelper.CreateBitmap(video.LargestImageUrl),
                        Stretch = Stretch.UniformToFill
                    });
                }
                catch (Exception ex) { Debug.WriteLine($"[PostMediaControl] Video thumbnail error: {ex.Message}"); }
            }

            // scrim keeps the title and the duration readable over any frame
            previewGrid.Children.Add(new Border
            {
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop { Color = Windows.UI.Color.FromArgb(0, 0, 0, 0), Offset = 0.45 },
                        new GradientStop { Color = Windows.UI.Color.FromArgb(190, 0, 0, 0), Offset = 1 }
                    }, 90)
            });

            var playButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 62,
                Height = 62,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(31),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(190, 0, 0, 0)),
                Content = new FontIcon
                {
                    Glyph = "\uE768",
                    FontSize = 24,
                    Foreground = new SolidColorBrush(Colors.White)
                }
            };
            ToolTipService.SetToolTip(playButton, "\u0412\u043E\u0441\u043F\u0440\u043E\u0438\u0437\u0432\u0435\u0441\u0442\u0438 \u0432\u0438\u0434\u0435\u043E");
            previewGrid.Children.Add(playButton);

            if (!string.IsNullOrWhiteSpace(video.Title))
            {
                previewGrid.Children.Add(new TextBlock
                {
                    Text = video.Title,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(14, 0, 80, 12)
                });
            }

            if (video.Duration > 0)
            {
                previewGrid.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(190, 0, 0, 0)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 3, 7, 3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 12, 12),
                    Child = new TextBlock
                    {
                        Text = FormatDuration(video.Duration),
                        FontSize = 12,
                        Foreground = new SolidColorBrush(Colors.White)
                    }
                });
            }

            var previewFrame = Rounded(previewGrid, 12);
            videoHost.Children.Add(previewFrame);

            playButton.Click += async (s, e) =>
            {
                // hide the frame, not just its content, or the rounded plate stays behind
                previewFrame.Visibility = Visibility.Collapsed;
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
                        Width = MediaMaxWidth,
                        Height = MediaMaxWidth * 9 / 16,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    videoHost.Children.Add(Rounded(mediaPlayerElement, 12));
                }
                else
                {
                    try
                    {
                        var webView = new WebView2
                        {
                            Source = new Uri(playerUrl),
                            Width = MediaMaxWidth,
                            Height = MediaMaxWidth * 9 / 16,
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        await webView.EnsureCoreWebView2Async();
                        videoHost.Children.Add(Rounded(webView, 12));
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
