using System;

namespace CustomUtils.Utilities.Reminders;

public enum ReminderStatus
{
    Pending,
    Fired,
    Dismissed,
}

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Note { get; set; }
    public DateTime FireAt { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;
}
