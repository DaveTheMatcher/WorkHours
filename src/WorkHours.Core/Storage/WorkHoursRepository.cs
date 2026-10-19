using WorkHours.Core.Models;
using WorkHours.Core.Services;

namespace WorkHours.Core.Storage;

/// <summary>
/// Punto unico di accesso ai dati: li tiene in memoria e li salva tutti insieme
/// a ogni modifica (sono pochi KB anche dopo anni di utilizzo).
/// </summary>
public sealed class WorkHoursRepository(IKeyValueStore store)
{
    public const string StorageKey = "workhours.data";
    public const string CorruptKeyPrefix = "workhours.data.corrupt.";
    public const string LastBackupKey = "workhours.lastBackup";

    private AppData _data = new();
    private bool _loaded;

    /// <summary>Scatta dopo ogni salvataggio o importazione, per aggiornare le pagine.</summary>
    public event Action? Changed;

    /// <summary>Valorizzato se all'avvio i dati salvati erano illeggibili (sono stati messi da parte, non cancellati).</summary>
    public string? LoadWarning { get; private set; }

    public Settings Settings => _data.Settings;

    public IReadOnlyList<WorkDay> Days => _data.Days;

    public async Task LoadAsync()
    {
        if (_loaded)
            return;

        var json = await store.GetAsync(StorageKey);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                _data = AppData.FromJson(json);
            }
            catch (InvalidBackupException ex)
            {
                // Non si perde niente: il contenuto illeggibile resta in una chiave a parte.
                await store.SetAsync(CorruptKeyPrefix + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), json);
                _data = new AppData();
                LoadWarning = $"I dati salvati non erano leggibili e sono stati messi da parte ({ex.Message}).";
            }
        }
        if (DateTime.TryParse(await store.GetAsync(LastBackupKey), null, System.Globalization.DateTimeStyles.RoundtripKind, out var last))
            LastBackup = last;

        _loaded = true;
    }

    /// <summary>Quando è stato esportato l'ultimo backup (null = mai). Non fa parte del backup stesso.</summary>
    public DateTime? LastBackup { get; private set; }

    /// <summary>True se ci sono dati e l'ultimo backup è più vecchio di <paramref name="maxAge"/> (o non c'è mai stato).</summary>
    public bool IsBackupDue(DateTime now, TimeSpan maxAge) =>
        _data.Days.Count > 0 && (LastBackup is null || now - LastBackup.Value > maxAge);

    public async Task MarkBackupDoneAsync(DateTime now)
    {
        LastBackup = now;
        await store.SetAsync(LastBackupKey, now.ToString("O"));
        Changed?.Invoke();
    }

    public WorkDay? GetDay(DateOnly date) => _data.Days.FirstOrDefault(d => d.Date == date);

    public BalanceCalculator CreateCalculator() => new(_data.Settings, _data.Days);

    /// <summary>
    /// Salva un giorno. Restituisce gli errori di validazione (e in quel caso non salva).
    /// Un giorno senza fasce, senza tipo e senza note viene eliminato.
    /// </summary>
    public async Task<IReadOnlyList<string>> SaveDayAsync(WorkDay day)
    {
        var errors = day.Validate();
        if (errors.Count > 0)
            return errors;

        var copy = new WorkDay
        {
            Date = day.Date,
            Slots = day.Slots.OrderBy(s => s.Start).ToList(),
            Type = day.Type,
            Note = string.IsNullOrWhiteSpace(day.Note) ? null : day.Note.Trim(),
        };

        _data.Days.RemoveAll(d => d.Date == day.Date);
        if (copy.Slots.Count > 0 || copy.Type is not null || copy.Note is not null)
        {
            _data.Days.Add(copy);
            _data.Days.Sort((a, b) => a.Date.CompareTo(b.Date));
        }

        await PersistAsync();
        return [];
    }

    public async Task DeleteDayAsync(DateOnly date)
    {
        if (_data.Days.RemoveAll(d => d.Date == date) > 0)
            await PersistAsync();
    }

    public async Task SaveSettingsAsync(Settings settings)
    {
        _data.Settings = settings;
        await PersistAsync();
    }

    public string ExportJson() => _data.ToJson();

    /// <summary>Sostituisce tutti i dati con quelli del backup. Se il backup non è valido non cambia nulla.</summary>
    public async Task ImportJsonAsync(string json)
    {
        var imported = AppData.FromJson(json);
        imported.Days.Sort((a, b) => a.Date.CompareTo(b.Date));
        _data = imported;
        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        await store.SetAsync(StorageKey, _data.ToJson());
        Changed?.Invoke();
    }
}
