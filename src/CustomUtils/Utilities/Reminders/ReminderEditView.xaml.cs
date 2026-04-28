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

        PopulateStaticDropdowns();

        if (existing is null)
        {
            HeaderText.Text = "New reminder";
            var when = RoundToNext5Minutes(DateTime.Now.AddHours(1));
            ApplyDateTime(when);
        }
        else
        {
            HeaderText.Text = "Edit reminder";
            TitleBox.Text = existing.Title;
            NoteBox.Text = existing.Note ?? "";
            ApplyDateTime(existing.FireAt);
        }

        Loaded += (_, _) => TitleBox.Focus();
    }

    private static DateTime RoundToNext5Minutes(DateTime t)
    {
        var minutes = (t.Minute / 5 + 1) * 5;
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, 0, 0).AddMinutes(minutes);
    }

    private void PopulateStaticDropdowns()
    {
        _suppressSync = true;

        // Years: this year through +10
        var thisYear = DateTime.Now.Year;
        for (int y = thisYear; y <= thisYear + 10; y++)
            YearCombo.Items.Add(y);

        // Months: 1-12 with names
        for (int m = 1; m <= 12; m++)
        {
            MonthCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{m:D2} – {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)}",
                Tag = m,
            });
        }

        // Hours 0-23
        for (int h = 0; h < 24; h++)
            HourCombo.Items.Add(h.ToString("D2"));

        // Minutes 0-59
        for (int n = 0; n < 60; n++)
            MinuteCombo.Items.Add(n.ToString("D2"));

        _suppressSync = false;
    }

    private void RebuildDayCombo(int year, int month, int? selectDay)
    {
        var prev = _suppressSync;
        _suppressSync = true;

        var days = DateTime.DaysInMonth(year, month);
        DayCombo.Items.Clear();
        for (int d = 1; d <= days; d++)
            DayCombo.Items.Add(d.ToString("D2"));

        var target = selectDay ?? 1;
        if (target > days) target = days;
        DayCombo.SelectedIndex = target - 1;

        _suppressSync = prev;
    }

    private void ApplyDateTime(DateTime dt)
    {
        _suppressSync = true;

        // Year
        var yearIdx = YearCombo.Items.IndexOf(dt.Year);
        if (yearIdx < 0)
        {
            YearCombo.Items.Insert(0, dt.Year);
            yearIdx = 0;
        }
        YearCombo.SelectedIndex = yearIdx;

        // Month
        MonthCombo.SelectedIndex = dt.Month - 1;

        // Day (depends on year/month)
        RebuildDayCombo(dt.Year, dt.Month, dt.Day);

        // Time
        HourCombo.SelectedIndex = dt.Hour;
        MinuteCombo.SelectedIndex = dt.Minute;

        // Calendar
        DateCalendar.SelectedDate = dt.Date;
        DateCalendar.DisplayDate = dt.Date;

        _suppressSync = false;
    }

    private DateTime? ReadFromDropdowns()
    {
        if (YearCombo.SelectedItem is not int year) return null;
        if (MonthCombo.SelectedItem is not ComboBoxItem mItem || mItem.Tag is not int month) return null;
        if (DayCombo.SelectedItem is not string dayStr || !int.TryParse(dayStr, out var day)) return null;
        if (HourCombo.SelectedItem is not string hourStr || !int.TryParse(hourStr, out var hour)) return null;
        if (MinuteCombo.SelectedItem is not string minStr || !int.TryParse(minStr, out var minute)) return null;

        try { return new DateTime(year, month, day, hour, minute, 0); }
        catch { return null; }
    }

    private void DatePart_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync) return;
        if (YearCombo.SelectedItem is not int year) return;
        if (MonthCombo.SelectedItem is not ComboBoxItem mItem || mItem.Tag is not int month) return;

        // If year/month changed, rebuild Day list (days-in-month varies).
        if (sender == YearCombo || sender == MonthCombo)
        {
            int? keep = null;
            if (DayCombo.SelectedItem is string ds && int.TryParse(ds, out var d)) keep = d;
            RebuildDayCombo(year, month, keep);
        }

        var dt = ReadFromDropdowns();
        if (dt is null) return;

        _suppressSync = true;
        DateCalendar.SelectedDate = dt.Value.Date;
        DateCalendar.DisplayDate = dt.Value.Date;
        _suppressSync = false;
    }

    private void TimePart_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Time dropdowns don't affect the calendar, so nothing to sync.
    }

    private void DateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSync) return;
        if (DateCalendar.SelectedDate is not { } d) return;

        _suppressSync = true;

        var yearIdx = YearCombo.Items.IndexOf(d.Year);
        if (yearIdx < 0)
        {
            YearCombo.Items.Insert(0, d.Year);
            yearIdx = 0;
        }
        YearCombo.SelectedIndex = yearIdx;
        MonthCombo.SelectedIndex = d.Month - 1;
        RebuildDayCombo(d.Year, d.Month, d.Day);

        _suppressSync = false;
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

        var dt = ReadFromDropdowns();
        if (dt is null)
        {
            error = "Please pick a valid date and time.";
            return false;
        }

        fireAt = dt.Value;
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
}
