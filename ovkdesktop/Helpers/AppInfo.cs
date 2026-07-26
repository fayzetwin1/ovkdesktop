namespace ovkdesktop.Helpers
{
    /// <summary>
    /// Single place for the user-facing product strings. They used to be repeated as
    /// literals on the welcome and the settings page, which is how they drift apart.
    /// </summary>
    public static class AppInfo
    {
        public const string ProductName = "OpenVK Desktop";

        /// <summary>
        /// Marketing version shown in the UI. Deliberately separate from the assembly
        /// version, which the csproj keeps at 1.0.
        /// </summary>
        public const string DisplayVersion = "2.5 Release";

        public const string Author = "Fayzetwin";

        public static string VersionLine => $"{ProductName}, version: {DisplayVersion}. Made with love by {Author} :3";
    }
}
