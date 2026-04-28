using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CustomUtils.Utilities.Reminders;

public sealed class ReminderStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly object _lock = new();
    private List<Reminder> _items = new();

    public ReminderStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CustomUtils");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "reminders.json");
        Load();
    }

    public IReadOnlyList<Reminder> All()
    {
        lock (_lock) return _items.ToList();
    }

    public Reminder? Get(Guid id)
    {
        lock (_lock) return _items.FirstOrDefault(r => r.Id == id);
    }

    public void Upsert(Reminder reminder)
    {
        lock (_lock)
        {
            var idx = _items.FindIndex(r => r.Id == reminder.Id);
            if (idx >= 0) _items[idx] = reminder;
            else _items.Add(reminder);
            Save();
        }
    }

    public void Delete(Guid id)
    {
        lock (_lock)
        {
            _items.RemoveAll(r => r.Id == id);
            Save();
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            _items = new List<Reminder>();
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            _items = JsonSerializer.Deserialize<List<Reminder>>(json, JsonOpts) ?? new List<Reminder>();
        }
        catch
        {
            // Corrupt file: back it up and start fresh rather than crash.
            try { File.Copy(_path, _path + ".bak", overwrite: true); } catch { }
            _items = new List<Reminder>();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_items, JsonOpts);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_path))
            File.Replace(tmp, _path, null);
        else
            File.Move(tmp, _path);
    }
}
