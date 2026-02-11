using System.Windows;

namespace MarkdownEditor;

public partial class App : Application
{
    public static bool IsDarkTheme { get; private set; } = true;

    public static void SwitchTheme(bool isDark)
    {
        IsDarkTheme = isDark;
        var themePath = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
        
        var newTheme = new ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        };

        var app = Current;
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(newTheme);
    }
}
