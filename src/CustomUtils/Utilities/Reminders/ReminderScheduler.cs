using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CustomUtils.Utilities.Reminders;

public sealed class ReminderScheduler : IDisposable
{
    private readonly ReminderStore _store;
    private readonly Action<Reminder> _fire;
    private readonly Dictionary<Guid, Timer> _timers = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event Action? Changed;

    public ReminderScheduler(ReminderStore store, Action<Reminder> fire)
    {
        _store = store;
        _fire = fire;
    }

    public void Start()
    {
        foreach (var r in _store.All().Where(r => r.Status == ReminderStatus.Pending))
            Schedule(r);
    }

    public void Schedule(Reminder reminder)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (_timers.Remove(reminder.Id, out var existing))
                existing.Dispose();

            if (reminder.Status != ReminderStatus.Pending) return;

            var due = reminder.FireAt - DateTime.Now;
            if (due <= TimeSpan.Zero)
            {
                ThreadPool.QueueUserWorkItem(_ => FireAndAdvance(reminder.Id));
                return;
            }

            // System.Threading.Timer caps at int.MaxValue ms (~24.8 days).
            // For longer waits, re-arm in chunks.
            var dueMs = (long)due.TotalMilliseconds;
            var firstTickMs = Math.Min(dueMs, int.MaxValue);

            Timer? t = null;
            t = new Timer(_ =>
            {
                lock (_lock)
                {
                    if (!_timers.TryGetValue(reminder.Id, out var stored) || !ReferenceEquals(stored, t))
                        return;
                    var remaining = reminder.FireAt - DateTime.Now;
                    if (remaining > TimeSpan.FromMilliseconds(500))
                    {
                        var next = (long)remaining.TotalMilliseconds;
                        try { t!.Change(Math.Min(next, int.MaxValue), Timeout.Infinite); } catch { }
                        return;
                    }
                    _timers.Remove(reminder.Id);
                    t!.Dispose();
                }
                FireAndAdvance(reminder.Id);
            }, null, firstTickMs, Timeout.Infinite);

            _timers[reminder.Id] = t;
        }
    }

    public void Cancel(Guid id)
    {
        lock (_lock)
        {
            if (_timers.Remove(id, out var t)) t.Dispose();
        }
    }

    private void FireAndAdvance(Guid id)
    {
        var reminder = _store.Get(id);
        if (reminder is null || reminder.Status != ReminderStatus.Pending) return;
        try { _fire(reminder); } catch { /* swallow so app keeps running */ }
        reminder.Status = ReminderStatus.Fired;
        _store.Upsert(reminder);
        Changed?.Invoke();
    }

    public void Snooze(Guid id, TimeSpan delay)
    {
        var reminder = _store.Get(id);
        if (reminder is null) return;
        reminder.FireAt = DateTime.Now + delay;
        reminder.Status = ReminderStatus.Pending;
        _store.Upsert(reminder);
        Schedule(reminder);
        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        Cancel(id);
        var reminder = _store.Get(id);
        if (reminder is null) return;
        reminder.Status = ReminderStatus.Dismissed;
        _store.Upsert(reminder);
        Changed?.Invoke();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            foreach (var t in _timers.Values) t.Dispose();
            _timers.Clear();
        }
    }
}
