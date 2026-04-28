using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CustomUtils.Utilities.Clipboard;
using CustomUtils.Utilities.Reminders;

namespace CustomUtils;

public partial class MainWindow : Window
{
    private RemindersView? _remindersView;
    private ClipboardHistoryView? _clipboardView;
    private SettingsView? _settingsView;

    public MainWindow()
    {
        InitializeComponent();
        ShowReminders();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Hide to tray instead of closing.
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void UtilityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // ContentHost can be null during XAML parsing, before InitializeComponent finishes.
        if (ContentHost is null) return;
        if (UtilityList?.SelectedItem is not ListBoxItem item) return;
        switch (item.Tag as string)
        {
            case "reminders": ShowReminders(); break;
            case "clipboard": ShowClipboard(); break;
            case "settings": ShowSettings(); break;
        }
    }

    private void ShowClipboard()
    {
        _clipboardView ??= new ClipboardHistoryView();
        ContentHost.Content = _clipboardView;
    }

    private void ShowReminders()
    {
        _remindersView ??= new RemindersView();
        ContentHost.Content = _remindersView;
    }

    private void ShowSettings()
    {
        _settingsView ??= new SettingsView();
        ContentHost.Content = _settingsView;
    }

    public void NavigateToReminder(Guid id)
    {
        // Switch to reminders pane and let it focus the row.
        if (UtilityList.Items.Count > 0)
        {
            foreach (var obj in UtilityList.Items)
            {
                if (obj is ListBoxItem li && (li.Tag as string) == "reminders")
                {
                    li.IsSelected = true;
                    break;
                }
            }
        }
        ShowReminders();
        _remindersView?.FocusReminder(id);
    }
}
