using System;
using System.Collections.Specialized;
using System.Web;
using CustomUtils.Utilities.Reminders;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;

namespace CustomUtils.Services;

public sealed class ToastService
{
    public event Action<Guid>? OpenRequested;
    public event Action<Guid, TimeSpan>? SnoozeRequested;
    public event Action<Guid>? DismissRequested;

    public void RegisterActivationHandler()
    {
        // Static OnActivated is needed for the case where the app is launched cold
        // by a toast click. While the app is already running (the common case for us
        // since it's tray-resident), per-toast Activated events do the work.
        ToastNotificationManagerCompat.OnActivated += OnStaticActivated;
    }

    public void Show(Reminder reminder)
    {
        var idArg = reminder.Id.ToString();
        var note = string.IsNullOrWhiteSpace(reminder.Note) ? "" : reminder.Note!;
        var when = reminder.FireAt.ToString("ddd, MMM d  h:mm tt");

        // Raw toast XML using Windows' built-in reminder scenario with system snooze
        // and dismiss actions. activationType="system" means Windows itself handles
        // those buttons — they don't route through the app's activation handler, so
        // they sidestep the unpackaged-Win32 toolkit quirk where user-defined buttons
        // can spuriously fire on toast timeout.
        var titleEsc = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(reminder.Title) ? "Reminder" : reminder.Title);
        var bodyEsc = System.Net.WebUtility.HtmlEncode(string.IsNullOrEmpty(note) ? when : $"{note}  ·  {when}");
        var launchEsc = System.Net.WebUtility.HtmlEncode($"action=open;id={idArg}");
        var rawXml = $@"<toast scenario=""reminder"" launch=""{launchEsc}"" activationType=""foreground"">
  <visual>
    <binding template=""ToastGeneric"">
      <text>{titleEsc}</text>
      <text>{bodyEsc}</text>
    </binding>
  </visual>
  <actions>
    <input id=""snoozeTime"" type=""selection"" defaultInput=""15"">
      <selection id=""5"" content=""5 minutes"" />
      <selection id=""15"" content=""15 minutes"" />
      <selection id=""60"" content=""1 hour"" />
      <selection id=""240"" content=""4 hours"" />
      <selection id=""1440"" content=""1 day"" />
    </input>
    <action activationType=""system"" arguments=""snooze"" hint-inputId=""snoozeTime"" content="""" />
    <action activationType=""system"" arguments=""dismiss"" content="""" />
  </actions>
</toast>";

        var xmlDoc = new Windows.Data.Xml.Dom.XmlDocument();
        xmlDoc.LoadXml(rawXml);

        var notif = new ToastNotification(xmlDoc)
        {
            Tag = Guid.NewGuid().ToString("N").Substring(0, 12),
            Group = "reminders",
        };

        notif.Activated += (_, args) => HandleActivation(ExtractArgs(args));

        ToastNotificationManagerCompat.CreateToastNotifier().Show(notif);
    }

    private static string ExtractArgs(object eventArgs)
    {
        var prop = eventArgs.GetType().GetProperty("Arguments");
        return prop?.GetValue(eventArgs) as string ?? "";
    }

    private void HandleActivation(string raw)
    {
        var args = ParseArgs(raw);
        var action = args["action"];
        if (string.IsNullOrEmpty(action)) return;
        if (!Guid.TryParse(args["id"], out var id)) return;

        switch (action)
        {
            case "open":
                OpenRequested?.Invoke(id);
                break;
            case "dismiss":
                DismissRequested?.Invoke(id);
                break;
            case "snooze":
                if (int.TryParse(args["minutes"], out var minutes))
                    SnoozeRequested?.Invoke(id, TimeSpan.FromMinutes(minutes));
                break;
        }
    }

    private void OnStaticActivated(ToastNotificationActivatedEventArgsCompat e)
        => HandleActivation(e.Argument);

    private static NameValueCollection ParseArgs(string raw)
    {
        var result = new NameValueCollection();
        if (string.IsNullOrEmpty(raw)) return result;
        foreach (var pair in raw.Split(new[] { ';', '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var k = HttpUtility.UrlDecode(pair.Substring(0, eq));
            var v = HttpUtility.UrlDecode(pair.Substring(eq + 1));
            result[k] = v;
        }
        return result;
    }
}
