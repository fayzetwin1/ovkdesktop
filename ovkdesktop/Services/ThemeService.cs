using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using ovkdesktop.Helpers;
using ovkdesktop.Models;

namespace ovkdesktop.Services
{
    /// <summary>
    /// Applies the accent colour by shadowing the WinUI accent theme resources with
    /// brushes this service owns. Because every consumer ends up holding a reference to
    /// the very same <see cref="SolidColorBrush"/> instance, changing the accent later is
    /// just a matter of assigning a new <see cref="SolidColorBrush.Color"/> — the whole UI
    /// repaints without a restart.
    /// </summary>
    public sealed class ThemeService : IThemeService
    {
        private readonly SettingsHelper _settings;

        // one shared brush per role - mutating Color is what makes live switching work
        private readonly SolidColorBrush _fill = new();
        private readonly SolidColorBrush _fillSecondary = new();
        private readonly SolidColorBrush _fillTertiary = new();
        private readonly SolidColorBrush _accentText = new();
        private readonly SolidColorBrush _accentTextTertiary = new();
        private readonly SolidColorBrush _onAccent = new();
        private readonly SolidColorBrush _onAccentSecondary = new();

        private FrameworkElement _root;
        private bool _resourcesInstalled;

        public ThemeService(SettingsHelper settings)
        {
            _settings = settings;
        }

        public AppThemeMode ThemeMode { get; private set; } = AppThemeMode.System;

        public string AccentId { get; private set; } = AccentPalette.SystemId;

        public Color AccentSeed { get; private set; } = AccentPalette.FallbackSeed;

        public IReadOnlyList<AccentPreset> Presets => AccentPalette.Presets;

        public event EventHandler ThemeChanged;

        public bool IsDarkEffective => ThemeMode switch
        {
            AppThemeMode.Dark => true,
            AppThemeMode.Light => false,
            _ => Application.Current?.RequestedTheme == ApplicationTheme.Dark
        };

        public ElementTheme ElementTheme => ThemeMode switch
        {
            AppThemeMode.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
            AppThemeMode.Light => Microsoft.UI.Xaml.ElementTheme.Light,
            _ => Microsoft.UI.Xaml.ElementTheme.Default
        };

        public void Initialize(FrameworkElement root)
        {
            _root = root;

            ThemeMode = _settings.ThemeMode;
            AccentId = string.IsNullOrWhiteSpace(_settings.AccentId)
                ? AccentPalette.SystemId
                : _settings.AccentId;

            AccentSeed = ResolveSeed(AccentId, ColorHelpers.TryParseHex(_settings.AccentCustomColor));

            InstallAccentResources();
            ApplyThemeMode();
            ApplyAccent();
        }

        public async Task SetThemeModeAsync(AppThemeMode mode)
        {
            if (ThemeMode == mode)
                return;

            ThemeMode = mode;
            _settings.ThemeMode = mode;

            ApplyThemeMode();
            // the accent shades depend on which palette is on screen
            ApplyAccent();

            await _settings.SaveAsync();
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task SetAccentAsync(string accentId, Color? customColor = null)
        {
            if (string.IsNullOrWhiteSpace(accentId))
                return;

            AccentId = accentId;
            _settings.AccentId = accentId;

            if (accentId == AccentPalette.CustomId && customColor.HasValue)
                _settings.AccentCustomColor = ColorHelpers.ToHex(customColor.Value);

            AccentSeed = ResolveSeed(accentId, customColor ?? ColorHelpers.TryParseHex(_settings.AccentCustomColor));

            ApplyAccent();

            await _settings.SaveAsync();
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyTo(FrameworkElement element)
        {
            if (element != null)
                element.RequestedTheme = ElementTheme;
        }

        private static Color ResolveSeed(string accentId, Color? customColor)
        {
            if (accentId == AccentPalette.CustomId)
                return customColor ?? AccentPalette.FallbackSeed;

            if (accentId == AccentPalette.SystemId)
                return ReadSystemAccent() ?? AccentPalette.FallbackSeed;

            return AccentPalette.FindPreset(accentId)?.Seed ?? AccentPalette.FallbackSeed;
        }

        /// <summary>
        /// reads the accent colour configured in the OS; not every platform Uno runs on
        /// exposes one, so failures fall back to the built-in default
        /// </summary>
        private static Color? ReadSystemAccent()
        {
            try
            {
                var uiSettings = new Windows.UI.ViewManagement.UISettings();
                var accent = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);

                // a fully transparent result means the platform has nothing to offer
                return accent.A == 0 ? null : Color.FromArgb(255, accent.R, accent.G, accent.B);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThemeService] Could not read the system accent colour: {ex.Message}");
                return null;
            }
        }

        private void ApplyThemeMode()
        {
            if (_root != null)
                _root.RequestedTheme = ElementTheme;

            App.MainWindowInstance?.ApplyTitleBarTheme(IsDarkEffective);
        }

        private void ApplyAccent()
        {
            bool isDark = IsDarkEffective;

            // WinUI derives its accent resources from SystemAccentColorLight2 on the dark
            // palette and SystemAccentColorDark1 on the light one; do the same so that a
            // user-picked colour behaves like a real Windows accent
            Color fill = isDark
                ? ColorHelpers.AdjustLightness(AccentSeed, 0.10)
                : ColorHelpers.AdjustLightness(AccentSeed, -0.06);

            Color pageSurface = isDark
                ? Color.FromArgb(255, 0x11, 0x13, 0x18)
                : Color.FromArgb(255, 0xF5, 0xF6, 0xF8);

            // text drawn straight onto the page needs more contrast than a filled button
            Color accentText = ColorHelpers.EnsureReadableOn(
                isDark ? ColorHelpers.AdjustLightness(AccentSeed, 0.20) : ColorHelpers.AdjustLightness(AccentSeed, -0.12),
                pageSurface);

            _fill.Color = fill;
            _fillSecondary.Color = ColorHelpers.Blend(fill, pageSurface, 0.90);
            _fillTertiary.Color = ColorHelpers.Blend(fill, pageSurface, 0.80);

            _accentText.Color = accentText;
            _accentTextTertiary.Color = fill;

            _onAccent.Color = ColorHelpers.ContrastingForeground(fill);
            _onAccentSecondary.Color = Color.FromArgb(0xB0, _onAccent.Color.R, _onAccent.Color.G, _onAccent.Color.B);

            UpdateAccentColorKeys();
        }

        private void InstallAccentResources()
        {
            if (_resourcesInstalled)
                return;

            var resources = Application.Current?.Resources;
            if (resources == null)
                return;

            Assign(resources, FillKeys, _fill);
            Assign(resources, FillSecondaryKeys, _fillSecondary);
            Assign(resources, FillTertiaryKeys, _fillTertiary);
            Assign(resources, AccentTextKeys, _accentText);
            Assign(resources, AccentTextTertiaryKeys, _accentTextTertiary);
            Assign(resources, OnAccentKeys, _onAccent);
            Assign(resources, OnAccentSecondaryKeys, _onAccentSecondary);

            _resourcesInstalled = true;
        }

        private static void Assign(ResourceDictionary resources, string[] keys, Brush brush)
        {
            foreach (var key in keys)
            {
                try
                {
                    resources[key] = brush;
                }
                catch (Exception ex)
                {
                    // a key missing from this WinUI/Uno version must not take the app down
                    Debug.WriteLine($"[ThemeService] Could not override '{key}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Colour (as opposed to Brush) resources cannot be mutated in place, so these are
        /// re-assigned on every accent change. Anything that resolved them earlier keeps the
        /// old value until the next launch, which is why the client only relies on the
        /// brush keys above for its own UI.
        /// </summary>
        private void UpdateAccentColorKeys()
        {
            var resources = Application.Current?.Resources;
            if (resources == null)
                return;

            var colors = new Dictionary<string, Color>
            {
                ["SystemAccentColor"] = AccentSeed,
                ["SystemAccentColorLight1"] = ColorHelpers.AdjustLightness(AccentSeed, 0.08),
                ["SystemAccentColorLight2"] = ColorHelpers.AdjustLightness(AccentSeed, 0.16),
                ["SystemAccentColorLight3"] = ColorHelpers.AdjustLightness(AccentSeed, 0.24),
                ["SystemAccentColorDark1"] = ColorHelpers.AdjustLightness(AccentSeed, -0.08),
                ["SystemAccentColorDark2"] = ColorHelpers.AdjustLightness(AccentSeed, -0.16),
                ["SystemAccentColorDark3"] = ColorHelpers.AdjustLightness(AccentSeed, -0.24),
                ["AccentFillColorDefault"] = _fill.Color,
                ["AccentFillColorSecondary"] = _fillSecondary.Color,
                ["AccentFillColorTertiary"] = _fillTertiary.Color,
                ["AccentTextFillColorPrimary"] = _accentText.Color,
                ["AccentTextFillColorSecondary"] = _accentText.Color,
                ["AccentTextFillColorTertiary"] = _accentTextTertiary.Color,
                ["TextOnAccentFillColorPrimary"] = _onAccent.Color,
                ["TextOnAccentFillColorSecondary"] = _onAccentSecondary.Color,
            };

            foreach (var pair in colors)
            {
                try
                {
                    resources[pair.Key] = pair.Value;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ThemeService] Could not override colour '{pair.Key}': {ex.Message}");
                }
            }
        }

        // --- the WinUI resource keys that make up "the accent colour" ---

        private static readonly string[] FillKeys =
        {
            "AccentFillColorDefaultBrush",
            "AccentControlElevationBorderBrush",
            "AccentButtonBackground",
            "AccentButtonBorderBrush",
            "ToggleSwitchFillOn",
            "ToggleSwitchStrokeOn",
            "CheckBoxCheckBackgroundFillChecked",
            "CheckBoxCheckBackgroundStrokeChecked",
            "RadioButtonOuterEllipseCheckedFill",
            "RadioButtonOuterEllipseCheckedStroke",
            "SliderTrackValueFill",
            "SliderThumbBackground",
            "ProgressBarForeground",
            "ProgressRingForeground",
            "NavigationViewSelectionIndicatorForeground",
            "TabViewSelectedItemBorderBrush",
            "TextControlSelectionHighlightColor",
            "SystemControlBackgroundAccentBrush",
            "SystemControlForegroundAccentBrush",
            "SystemControlHighlightAccentBrush",
            "SystemControlHighlightAltAccentBrush",
            "SystemControlHighlightAltListAccentHighBrush",
            "SystemControlHyperlinkTextBrush",
            "InfoBarInformationalSeverityIconBackground",
        };

        private static readonly string[] FillSecondaryKeys =
        {
            "AccentFillColorSecondaryBrush",
            "AccentButtonBackgroundPointerOver",
            "ToggleSwitchFillOnPointerOver",
            "ToggleSwitchStrokeOnPointerOver",
            "CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeCheckedPointerOver",
            "RadioButtonOuterEllipseCheckedFillPointerOver",
            "RadioButtonOuterEllipseCheckedStrokePointerOver",
            "SliderTrackValueFillPointerOver",
            "SliderThumbBackgroundPointerOver",
            "SystemControlHighlightListAccentLowBrush",
            "SystemControlHighlightListAccentMediumBrush",
        };

        private static readonly string[] FillTertiaryKeys =
        {
            "AccentFillColorTertiaryBrush",
            "AccentButtonBackgroundPressed",
            "ToggleSwitchFillOnPressed",
            "ToggleSwitchStrokeOnPressed",
            "CheckBoxCheckBackgroundFillCheckedPressed",
            "CheckBoxCheckBackgroundStrokeCheckedPressed",
            "RadioButtonOuterEllipseCheckedFillPressed",
            "RadioButtonOuterEllipseCheckedStrokePressed",
            "SliderTrackValueFillPressed",
            "SliderThumbBackgroundPressed",
            "SystemControlHighlightListAccentHighBrush",
        };

        private static readonly string[] AccentTextKeys =
        {
            "AccentTextFillColorPrimaryBrush",
            "AccentTextFillColorSecondaryBrush",
            "HyperlinkButtonForeground",
            "HyperlinkForeground",
            "PivotHeaderItemForegroundSelected",
            "PivotHeaderItemForegroundSelectedPointerOver",
            "PivotHeaderItemForegroundSelectedPressed",
        };

        private static readonly string[] AccentTextTertiaryKeys =
        {
            "AccentTextFillColorTertiaryBrush",
            "HyperlinkButtonForegroundPointerOver",
            "HyperlinkButtonForegroundPressed",
        };

        private static readonly string[] OnAccentKeys =
        {
            "TextOnAccentFillColorPrimaryBrush",
            "TextOnAccentFillColorSelectedTextBrush",
            "AccentButtonForeground",
            "AccentButtonForegroundPointerOver",
            "AccentButtonForegroundPressed",
            "ToggleSwitchKnobFillOn",
            "ToggleSwitchKnobFillOnPointerOver",
            "ToggleSwitchKnobFillOnPressed",
            "CheckBoxCheckGlyphForegroundChecked",
            "CheckBoxCheckGlyphForegroundCheckedPointerOver",
            "CheckBoxCheckGlyphForegroundCheckedPressed",
        };

        private static readonly string[] OnAccentSecondaryKeys =
        {
            "TextOnAccentFillColorSecondaryBrush",
        };
    }
}
