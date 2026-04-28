using System.Windows;
using System.Windows.Controls;
using CustomUtils.Services;

namespace CustomUtils;

public partial class SettingsView : UserControl
{
    private bool _loading;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        _loading = true;
        AutoStartCheckBox.IsChecked = StartupService.IsEnabled();
        DarkModeCheckBox.IsChecked = App.Theme.IsDark;
        _loading = false;
    }

    private void AutoStart_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        try
        {
            if (AutoStartCheckBox.IsChecked == true) StartupService.Enable();
            else StartupService.Disable();
        }
        catch
        {
            // Reflect the actual state if the registry write failed.
            Refresh();
        }
    }

    private void DarkMode_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var dark = DarkModeCheckBox.IsChecked == true;
        App.Theme.Apply(dark);
        App.Settings.DarkMode = dark;
        try { App.Settings.Save(); } catch { }
    }
}
