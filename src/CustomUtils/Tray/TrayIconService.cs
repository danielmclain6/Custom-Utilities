using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace CustomUtils.Tray;

public sealed class TrayIconService : IDisposable
{
    private TaskbarIcon? _icon;

    public event Action? OpenRequested;
    public event Action? QuitRequested;

    public void Initialize()
    {
        _icon = new TaskbarIcon
        {
            ToolTipText = "Custom Utilities",
        };

        var iconUri = new Uri("pack://application:,,,/Assets/icon.ico", UriKind.Absolute);
        try { _icon.IconSource = new BitmapImage(iconUri); } catch { /* fall back to system default */ }

        _icon.TrayLeftMouseUp += (_, _) => OpenRequested?.Invoke();
        _icon.NoLeftClickDelay = true;

        var menu = new ContextMenu();

        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => OpenRequested?.Invoke();
        menu.Items.Add(open);

        menu.Items.Add(new Separator());

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) => QuitRequested?.Invoke();
        menu.Items.Add(quit);

        _icon.ContextMenu = menu;
        _icon.ForceCreate();
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }
}
