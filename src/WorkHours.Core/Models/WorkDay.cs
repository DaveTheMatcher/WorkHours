namespace WorkHours.Core.Models;

/// <summary>
/// Quanto si è lavorato in un giorno. Due modi di inserimento:
/// - nuovo: tempo di presenza e pausa (lavorato = presenza − pausa);
/// - vecchio: fasce orarie (lavorato = somma delle fasce), per i giorni inseriti prima del cambio.
/// Un giorno usa l'uno o l'altro, mai entrambi.
/// </summary>
public sealed class WorkDay
{
    private static readonly TimeSpan MaxDay = TimeSpan.FromHours(24);

    public DateOnly Date { get; set; }

    /// <summary>Fasce orarie (formato vecchio). Vuoto nei giorni inseriti con presenza e pausa.</summary>
    public List<TimeSlot> Slots { get; set; } = [];

    /// <summary>Tempo totale di presenza, pausa compresa. Null nei giorni a fasce.</summary>
    public TimeSpan? Presence { get; set; }

    /// <summary>Pausa da togliere alla presenza. Null = nessuna pausa.</summary>
    public TimeSpan? Pause { get; set; }

    /// <summary>
    /// Tipo scelto a mano. Null = automatico: Festivo se il giorno è festivo, altrimenti Lavoro.
    /// Impostare Lavoro su un festivo serve a "togliere" il festivo.
    /// </summary>
    public DayType? Type { get; set; }

    public string? Note { get; set; }

    /// <summary>Vero se il giorno è stato inserito con le fasce orarie (formato vecchio).</summary>
    public bool UsesSlots => Presence is null && Slots.Count > 0;

    public TimeSpan Worked => Presence is { } presence
        ? Max(presence - (Pause ?? TimeSpan.Zero), TimeSpan.Zero)
        : Slots.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

    /// <summary>
    /// Presenza e pausa equivalenti alle fasce: presenza dall'inizio della prima alla fine dell'ultima,
    /// pausa = i buchi tra una fascia e l'altra. Il lavorato resta identico.
    /// Per un giorno già nel formato nuovo restituisce i suoi valori.
    /// </summary>
    public (TimeSpan Presence, TimeSpan Pause) ToPresenceAndPause()
    {
        if (Presence is { } presence)
            return (presence, Pause ?? TimeSpan.Zero);

        var valid = Slots.Where(s => s.IsValid).ToList();
        if (valid.Count == 0)
            return (TimeSpan.Zero, TimeSpan.Zero);

        var span = valid.Max(s => s.End) - valid.Min(s => s.Start);
        return (span, span - Worked);
    }

    /// <summary>Errori di inserimento. Vuoto se tutto ok.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Presence is { } presence)
        {
            var pause = Pause ?? TimeSpan.Zero;
            if (Slots.Count > 0)
                errors.Add("Il giorno ha sia le fasce orarie sia la presenza: ne serve solo una.");
            if (presence < TimeSpan.Zero || pause < TimeSpan.Zero)
                errors.Add("Presenza e pausa non possono essere negative.");
            if (presence > MaxDay)
                errors.Add("La presenza non può superare 24 ore.");
            if (pause > presence)
                errors.Add("La pausa non può essere più lunga della presenza.");
            return errors;
        }

        if (Pause is not null)
            errors.Add("C'è una pausa ma manca la presenza.");

        foreach (var s in Slots.Where(s => !s.IsValid))
            errors.Add($"La fascia {s.Start:HH\\:mm}–{s.End:HH\\:mm} finisce prima di iniziare.");

        var ordered = Slots.Where(s => s.IsValid).OrderBy(s => s.Start).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Overlaps(ordered[i - 1]))
                errors.Add($"Le fasce {ordered[i - 1].Start:HH\\:mm}–{ordered[i - 1].End:HH\\:mm} e {ordered[i].Start:HH\\:mm}–{ordered[i].End:HH\\:mm} si sovrappongono.");
        }
        return errors;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
