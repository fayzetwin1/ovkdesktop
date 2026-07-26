using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI;

namespace ovkdesktop.Models
{
    /// <summary>
    /// how the client picks between the light and the dark palette
    /// </summary>
    public enum AppThemeMode
    {
        System = 0,
        Light = 1,
        Dark = 2
    }

    /// <summary>
    /// a named accent colour the user can pick in the settings
    /// </summary>
    public sealed class AccentPreset
    {
        public AccentPreset(string id, string name, Color seed)
        {
            Id = id;
            Name = name;
            Seed = seed;
        }

        /// <summary>stable identifier stored in the config file</summary>
        public string Id { get; }

        /// <summary>display name shown in the settings page</summary>
        public string Name { get; }

        /// <summary>base colour; the light/dark shades are derived from it</summary>
        public Color Seed { get; }
    }

    /// <summary>
    /// the accent colours shipped with the client
    /// </summary>
    public static class AccentPalette
    {
        /// <summary>follow the accent colour configured in the operating system</summary>
        public const string SystemId = "system";

        /// <summary>use the colour the user picked manually instead of a preset</summary>
        public const string CustomId = "custom";

        /// <summary>fallback used when the OS accent cannot be read</summary>
        public static readonly Color FallbackSeed = Color.FromArgb(255, 0x2F, 0x80, 0xED);

        public static IReadOnlyList<AccentPreset> Presets { get; } = new List<AccentPreset>
        {
            new("violet",   "Фиолетовый",  Color.FromArgb(255, 0x8B, 0x5C, 0xF6)),
            new("indigo",   "Индиго",      Color.FromArgb(255, 0x63, 0x66, 0xF1)),
            new("blue",     "Синий",       Color.FromArgb(255, 0x2F, 0x80, 0xED)),
            new("cyan",     "Голубой",     Color.FromArgb(255, 0x06, 0xB6, 0xD4)),
            new("teal",     "Бирюзовый",   Color.FromArgb(255, 0x14, 0xB8, 0xA6)),
            new("green",    "Зелёный",     Color.FromArgb(255, 0x22, 0xC5, 0x5E)),
            new("lime",     "Лаймовый",    Color.FromArgb(255, 0x84, 0xCC, 0x16)),
            new("amber",    "Янтарный",    Color.FromArgb(255, 0xF0, 0xA7, 0x0A)),
            new("orange",   "Оранжевый",   Color.FromArgb(255, 0xF9, 0x73, 0x16)),
            new("red",      "Красный",     Color.FromArgb(255, 0xEF, 0x44, 0x44)),
            new("pink",     "Розовый",     Color.FromArgb(255, 0xEC, 0x48, 0x99)),
            new("magenta",  "Пурпурный",   Color.FromArgb(255, 0xD9, 0x46, 0xEF)),
            new("graphite", "Графитовый",  Color.FromArgb(255, 0x7C, 0x87, 0x97)),
        };

        public static AccentPreset FindPreset(string id) =>
            Presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
