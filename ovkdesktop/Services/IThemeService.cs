using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Windows.UI;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    /// <summary>
    /// owns the light/dark palette and the accent colour of the client
    /// </summary>
    public interface IThemeService
    {
        /// <summary>light, dark, or whatever the operating system asks for</summary>
        AppThemeMode ThemeMode { get; }

        /// <summary>
        /// identifier of the selected accent: a preset id,
        /// <see cref="AccentPalette.SystemId"/>, or <see cref="AccentPalette.CustomId"/>
        /// </summary>
        string AccentId { get; }

        /// <summary>the seed colour currently in use (already resolved for system/custom)</summary>
        Color AccentSeed { get; }

        /// <summary>true when the dark palette is the one actually on screen</summary>
        bool IsDarkEffective { get; }

        /// <summary>the theme to hand to elements that live outside the main visual tree</summary>
        ElementTheme ElementTheme { get; }

        IReadOnlyList<AccentPreset> Presets { get; }

        event EventHandler ThemeChanged;

        /// <summary>
        /// installs the accent resources and applies the stored preferences.
        /// must run before the first page is shown.
        /// </summary>
        void Initialize(FrameworkElement root);

        Task SetThemeModeAsync(AppThemeMode mode);

        /// <summary>
        /// switches the accent. <paramref name="customColor"/> is only used when
        /// <paramref name="accentId"/> is <see cref="AccentPalette.CustomId"/>.
        /// </summary>
        Task SetAccentAsync(string accentId, Color? customColor = null);

        /// <summary>
        /// pushes the current theme onto a dialog, flyout or any other element
        /// that is hosted in its own popup root and therefore does not inherit it
        /// </summary>
        void ApplyTo(FrameworkElement element);
    }
}
