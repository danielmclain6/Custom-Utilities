using System;
using System.Linq;
using System.Threading;
using System.Windows;
using CustomUtils.Services;
using CustomUtils.Tray;
using CustomUtils.Utilities.Clipboard;
using CustomUtils.Utilities.Reminders;

namespace CustomUtils;

public partial class App : Application
{
    private const string MutexName = @"Local\CustomUtils.SingleInstance.Mutex";
    private const string ShowEventName = @"Local\CustomUtils.ShowMainWindow.Event";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _showEvent;
    private Thread? _showWatcherThread;
    private bool _shuttingDown;

    public static ReminderStore Store { get; private set; } = null!;
    public static ReminderScheduler Scheduler { get; private set; } = null!;
    public static ToastService Toast { get; private set; } = null!;
    public static TrayIconService Tray { get; private set; } = null!;
    public static ClipboardStore ClipStore { get; private set; } = null!;
    public static ClipboardWatcher ClipWatcher { get; private set; } = null!;
    public static HotkeyService Hotkeys { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomUtils");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:O}] {ev.ExceptionObject}\n\n");
            }
            catch { }
        };
        DispatcherUnhandledException += (_, ev) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomUtils");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"),
                    $"[{DateTime.Now:O}] DISPATCHER {ev.Exception}\n\n");
            }
            catch { }
        };

        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // Another instance is running — signal it to show its window, then exit.
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var existing))
                {
                    existing.Set();
                    existing.Dispose();
                }
            }
            catch { /* ignore */ }
            Shutdown();
            return;
        }

        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWatcherThread = new Thread(ShowWatcherLoop) { IsBackground = true, Name = "CustomUtils.ShowWatcher" };
        _showWatcherThread.Start();

        var silent = e.Args.Any(a => string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase));

        Store = new ReminderStore();
        Toast = new ToastService();
        Toast.RegisterActivationHandler();

        Scheduler = new ReminderScheduler(Store, r => Toast.Show(r));
        Scheduler.Start();

        Tray = new TrayIconService();
        Tray.Initialize();
        Tray.OpenRequested += () => Dispatcher.Invoke(ShowMainWindow);
        Tray.QuitRequested += () => Dispatcher.Invoke(QuitApp);

        Toast.OpenRequested += id => Dispatcher.Invoke(() => ShowMainWindow(id));
        Toast.SnoozeRequested += (id, delay) => Scheduler.Snooze(id, delay);
        Toast.DismissRequested += id => Scheduler.Dismiss(id);

        ClipStore = new ClipboardStore();
        ClipWatcher = new ClipboardWatcher();
        ClipWatcher.TextCaptured += text => ClipStore.Capture(text);
        ClipWatcher.Start();

        Hotkeys = new HotkeyService();
        Hotkeys.Start();
        // Alt + Space → show main window. VK_SPACE = 0x20.
        Hotkeys.Register(HotkeyModifiers.Alt, 0x20, () => Dispatcher.Invoke(ShowMainWindow));

        // Default-on auto-start: enable the first time we ever run.
        if (!HasRunBefore())
        {
            try { StartupService.Enable(); } catch { /* ignore */ }
            MarkHasRun();
        }

        if (!silent)
        {
            ShowMainWindow();
        }
    }

    private void ShowWatcherLoop()
    {
        while (!_shuttingDown && _showEvent is not null)
        {
            try
            {
                if (_showEvent.WaitOne(500))
                {
                    Dispatcher.Invoke(ShowMainWindow);
                }
            }
            catch { break; }
        }
    }

    public void ShowMainWindow() => ShowMainWindow(null);

    public void ShowMainWindow(Guid? focusReminderId)
    {
        if (MainWindow is null)
        {
            MainWindow = new MainWindow();
        }
        if (!MainWindow.IsVisible) MainWindow.Show();
        if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
        MainWindow.Focus();

        if (focusReminderId is { } id && MainWindow is MainWindow mw)
            mw.NavigateToReminder(id);
    }

    public void QuitApp()
    {
        _shuttingDown = true;
        try { Scheduler?.Dispose(); } catch { }
        try { ClipWatcher?.Dispose(); } catch { }
        try { Hotkeys?.Dispose(); } catch { }
        try { Tray?.Dispose(); } catch { }
        try { _showEvent?.Set(); _showEvent?.Dispose(); } catch { }
        try { _instanceMutex?.ReleaseMutex(); } catch { }
        try { _instanceMutex?.Dispose(); } catch { }
        Shutdown();
    }

    private static bool HasRunBefore()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomUtils");
        return System.IO.File.Exists(System.IO.Path.Combine(dir, ".initialized"));
    }

    private static void MarkHasRun()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CustomUtils");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, ".initialized"), DateTime.Now.ToString("o"));
    }
}
