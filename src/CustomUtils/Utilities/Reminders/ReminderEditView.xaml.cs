using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CustomUtils.Utilities.Reminders;

public partial class ReminderEditView : UserControl
{
    private readonly Reminder? _editing;
    private bool _suppressSync;

    public event Action<Reminder?>? Done;

    public ReminderEditView() : this(null) { }

    public ReminderEditView(Reminder? existing)
    {
        InitializeComponent();
        _editing = existing;

        if (existing is null)
        {
            HeaderText.Text = "New reminder";
            // Default: 1 hour from now, rounded to next 5 minutes.
            var when = RoundToNext5Minutes(DateTime.Now.AddHours(1));
            SetDate(when.Date);
            SetTime(when);
        }
        else
        {
            HeaderText.Text = "Edit reminder";
            TitleBox.Text = existing.Title;
            NoteBox.Text = existing.Note ?? "";
            SetDate(existing.FireAt.Date);
            SetTime(existing.FireAt);
        }

        Loaded += (_, _) => TitleBox.Focus();
    }

    private static DateTime RoundToNext5Minutes(DateTime t)
    {
        var minutes = (t.Minute / 5 + 1) * 5;
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0).AddMinutes(minutes);
    }

    private void SetDate(DateTime d)
    {
        _suppressSync = true;
        DateBox.Text = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        DateCalendar.SelectedDate = d;
        DateCalendar.DisplayDate = d;
        _suppressSync = false;
    }

    private void SetTime(DateTime t)
    {
        TimeBox.Text = t.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    private void DateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync) return;
        if (DateCalendar.SelectedDate is { } d)
        {
            _suppressSync = true;
            DateBox.Text = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            _suppressSync = false;
        }
    }

    private void DateBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSync) return;
        if (DateTime.TryParseExact(DateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            _suppressSync = true;
            DateCalendar.SelectedDate = d;
            DateCalendar.DisplayDate = d;
            _suppressSync = false;
        }
    }

    private bool TryParseInputs(out DateTime fireAt, out string error)
    {
        error = "";
        fireAt = default;

        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            error = "Please enter a title.";
            return false;
        }

        if (!DateTime.TryParseExact(DateBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            error = "Date must be in yyyy-MM-dd format.";
            return false;
        }

        if (!TimeSpan.TryParseExact(TimeBox.Text, @"h\:mm", CultureInfo.InvariantCulture, out var t)
            && !TimeSpan.TryParseExact(TimeBox.Text, @"hh\:mm", CultureInfo.InvariantCulture, out t))
        {
            error = "Time must be in HH:mm format (e.g. 14:30).";
            return false;
        }

        fireAt = d.Add(t);
        if (fireAt <= DateTime.Now)
        {
            error = "Reminder must be in the future.";
            return false;
        }

        return true;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseInputs(out var fireAt, out var error))
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        ErrorText.Visibility = Visibility.Collapsed;

        var reminder = _editing ?? new Reminder();
        reminder.Title = TitleBox.Text.Trim();
        reminder.Note = string.IsNullOrWhiteSpace(NoteBox.Text) ? null : NoteBox.Text.Trim();
        reminder.FireAt = fireAt;
        reminder.Status = ReminderStatus.Pending;

        App.Store.Upsert(reminder);
        App.Scheduler.Schedule(reminder);
        Done?.Invoke(reminder);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Done?.Invoke(null);

    private void TimePlus15_Click(object sender, RoutedEventArgs e) => BumpTime(TimeSpan.FromMinutes(15));
    private void TimeMinus15_Click(object sender, RoutedEventArgs e) => BumpTime(TimeSpan.FromMinutes(-15));
    private void TimePlus1h_Click(object sender, RoutedEventArgs e) => BumpTime(TimeSpan.FromHours(1));

    private void BumpTime(TimeSpan delta)
    {
        if (!TimeSpan.TryParseExact(TimeBox.Text, @"h\:mm", CultureInfo.InvariantCulture, out var t)
            && !TimeSpan.TryParseExact(TimeBox.Text, @"hh\:mm", CultureInfo.InvariantCulture, out t))
        {
            t = DateTime.Now.TimeOfDay;
        }
        var combined = (DateCalendar.SelectedDate ?? DateTime.Today).Date.Add(t).Add(delta);
        SetDate(combined.Date);
        SetTime(combined);
    }
}
