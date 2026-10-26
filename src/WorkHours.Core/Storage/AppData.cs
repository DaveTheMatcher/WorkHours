using System.Text.Json;
using System.Text.Json.Serialization;
using WorkHours.Core.Models;

namespace WorkHours.Core.Storage;

/// <summary>Tutti i dati dell'app. È anche il formato del file di backup.</summary>
public sealed class AppData
{
    /// <summary>
    /// 1 = giorni solo a fasce orarie.
    /// 2 = aggiunti presenza e pausa per giorno; i giorni a fasce restano validi.
    /// </summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public Settings Settings { get; set; } = new();

    public List<WorkDay> Days { get; set; } = [];

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Legge e controlla un JSON. Lancia <see cref="InvalidBackupException"/> se non è valido.</summary>
    public static AppData FromJson(string json)
    {
        AppData? data;
        try
        {
            data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidBackupException("Il file non è un backup valido di WorkHours.", ex);
        }

        if (data is null)
            throw new InvalidBackupException("Il file di backup è vuoto.");

        data.Days ??= [];
        foreach (var day in data.Days)
            day.Slots ??= [];
        if (data.Version > CurrentVersion)
            throw new InvalidBackupException("Il backup è stato creato da una versione più recente dell'app.");
        if (data.Settings?.Schedules is not { Count: > 0 })
            throw new InvalidBackupException("Il backup non contiene il monte ore.");
        if (data.Days.GroupBy(d => d.Date).Any(g => g.Count() > 1))
            throw new InvalidBackupException("Il backup contiene lo stesso giorno più volte.");

        var invalid = data.Days.FirstOrDefault(d => d.Validate().Count > 0);
        if (invalid is not null)
            throw new InvalidBackupException($"Il giorno {invalid.Date:dd/MM/yyyy} ha ore non valide.");

        // Un backup vecchio letto da questa app diventa del formato attuale (i giorni a fasce restano tali).
        data.Version = CurrentVersion;
        return data;
    }
}

public sealed class InvalidBackupException(string message, Exception? inner = null) : Exception(message, inner);
