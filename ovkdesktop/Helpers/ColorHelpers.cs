using System;
using System.Globalization;
using Windows.UI;

namespace ovkdesktop.Helpers
{
    /// <summary>
    /// small colour maths used to derive the accent shades from a single seed colour
    /// </summary>
    public static class ColorHelpers
    {
        /// <summary>
        /// parses "#RRGGBB" / "#AARRGGBB" (the leading '#' is optional)
        /// </summary>
        public static Color? TryParseHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var hex = value.Trim().TrimStart('#');

            if (hex.Length == 6)
                hex = "FF" + hex;

            if (hex.Length != 8)
                return null;

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                return null;

            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        public static string ToHex(Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        /// <summary>
        /// shifts the lightness of a colour while keeping hue and saturation,
        /// which is how the SystemAccentColorLightN / DarkN shades are produced
        /// </summary>
        public static Color AdjustLightness(Color color, double delta)
        {
            RgbToHsl(color, out double h, out double s, out double l);
            return HslToRgb(h, s, Clamp01(l + delta), color.A);
        }

        /// <summary>
        /// blends <paramref name="color"/> over <paramref name="background"/> at the given opacity
        /// </summary>
        public static Color Blend(Color color, Color background, double opacity)
        {
            opacity = Clamp01(opacity);
            return Color.FromArgb(
                255,
                (byte)Math.Round(color.R * opacity + background.R * (1 - opacity)),
                (byte)Math.Round(color.G * opacity + background.G * (1 - opacity)),
                (byte)Math.Round(color.B * opacity + background.B * (1 - opacity)));
        }

        /// <summary>
        /// WCAG relative luminance, used to decide whether text on top of the
        /// accent colour should be black or white
        /// </summary>
        public static double RelativeLuminance(Color color)
        {
            static double Channel(byte raw)
            {
                double v = raw / 255.0;
                return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }

        /// <summary>
        /// returns black or white, whichever stays readable on top of <paramref name="background"/>
        /// </summary>
        public static Color ContrastingForeground(Color background) =>
            RelativeLuminance(background) > 0.42
                ? Color.FromArgb(255, 0, 0, 0)
                : Color.FromArgb(255, 255, 255, 255);

        /// <summary>
        /// nudges the lightness until the colour is readable on the given surface;
        /// keeps user-picked accents legible on both the light and the dark palette
        /// </summary>
        public static Color EnsureReadableOn(Color color, Color surface, double minContrast = 4.5)
        {
            bool surfaceIsDark = RelativeLuminance(surface) < 0.5;
            var result = color;

            // 24 steps of 2.5% lightness is enough to cross the whole range
            for (int i = 0; i < 24 && ContrastRatio(result, surface) < minContrast; i++)
                result = AdjustLightness(result, surfaceIsDark ? 0.025 : -0.025);

            return result;
        }

        public static double ContrastRatio(Color a, Color b)
        {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            double lighter = Math.Max(la, lb);
            double darker = Math.Min(la, lb);
            return (lighter + 0.05) / (darker + 0.05);
        }

        private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));

        private static void RgbToHsl(Color color, out double h, out double s, out double l)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            l = (max + min) / 2.0;

            if (delta == 0)
            {
                h = 0;
                s = 0;
                return;
            }

            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            if (max == r)
                h = ((g - b) / delta + (g < b ? 6 : 0)) / 6.0;
            else if (max == g)
                h = ((b - r) / delta + 2) / 6.0;
            else
                h = ((r - g) / delta + 4) / 6.0;
        }

        private static Color HslToRgb(double h, double s, double l, byte alpha)
        {
            if (s == 0)
            {
                byte v = (byte)Math.Round(l * 255);
                return Color.FromArgb(alpha, v, v, v);
            }

            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;

            return Color.FromArgb(
                alpha,
                (byte)Math.Round(HueToChannel(p, q, h + 1.0 / 3.0) * 255),
                (byte)Math.Round(HueToChannel(p, q, h) * 255),
                (byte)Math.Round(HueToChannel(p, q, h - 1.0 / 3.0) * 255));
        }

        private static double HueToChannel(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;

            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}
