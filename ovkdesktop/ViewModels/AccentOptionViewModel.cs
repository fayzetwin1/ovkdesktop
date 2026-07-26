using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using ovkdesktop.Models;

namespace ovkdesktop.ViewModels
{
    /// <summary>
    /// one swatch in the accent colour picker on the settings page
    /// </summary>
    public sealed class AccentOptionViewModel : ObservableObject
    {
        private bool _isSelected;
        private Color _color;

        public AccentOptionViewModel(string id, string name, Color color, string glyph = null)
        {
            Id = id;
            Name = name;
            Glyph = glyph;
            _color = color;
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>
        /// segoe glyph shown instead of a plain fill; used by the "system" and
        /// "custom" entries so they read as actions rather than colours
        /// </summary>
        public string Glyph { get; }

        public Visibility GlyphVisibility =>
            string.IsNullOrEmpty(Glyph) ? Visibility.Collapsed : Visibility.Visible;

        public Color Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(Fill));
                    OnPropertyChanged(nameof(GlyphBrush));
                }
            }
        }

        public Brush Fill => new SolidColorBrush(_color);

        /// <summary>black or white, whichever stays readable on top of the swatch</summary>
        public Brush GlyphBrush => new SolidColorBrush(Helpers.ColorHelpers.ContrastingForeground(_color));

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                    OnPropertyChanged(nameof(SelectionVisibility));
            }
        }

        public Visibility SelectionVisibility =>
            _isSelected ? Visibility.Visible : Visibility.Collapsed;
    }
}
