using Microsoft.UI.Xaml;

namespace ovkdesktop.Helpers
{
    public static class ThemeHelper
    {
        /// <summary>
        /// Pushes the client theme onto an element and returns it, so it can be chained
        /// onto an object initialiser.
        ///
        /// Dialogs and flyouts are hosted in the XamlRoot's popup root rather than inside
        /// the page tree, so they do not pick up the RequestedTheme that ThemeService sets
        /// on the root frame. Without this they stay dark after switching to the light
        /// palette.
        /// </summary>
        public static T Themed<T>(this T element) where T : FrameworkElement
        {
            App.ThemeService?.ApplyTo(element);
            return element;
        }
    }
}
