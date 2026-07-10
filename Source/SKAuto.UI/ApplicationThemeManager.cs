using System.Windows;

namespace SKAuto.UI
{
    public static class ApplicationThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            // Build the URI to the theme XAML file
            var uri = new Uri($"Styles/Themes/{themeName}Theme.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };

            // Remove any existing theme dictionary (identify by containing "Theme.xaml")
            var existing = System.Windows.Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Theme.xaml") == true);
            if (existing != null)
                System.Windows.Application.Current.Resources.MergedDictionaries.Remove(existing);

            // Add the new theme
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
        }

        public static string GetCurrentTheme()
        {
            var existing = System.Windows.Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains("Theme.xaml") == true);
            if (existing == null) return "Light";

            var fileName = System.IO.Path.GetFileNameWithoutExtension(existing.Source?.OriginalString ?? "");
            return fileName.Replace("Theme", "");
        }
    }
}