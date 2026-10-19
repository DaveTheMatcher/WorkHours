namespace WorkHours.Core.Models;

/// <summary>Quanto si è lavorato in un giorno.</summary>
public sealed class WorkDay
{
    public DateOnly Date { get; set; }

    public List<TimeSlot> Slots { get; set; } = [];

    /// <summary>
    /// Tipo scelto a mano. Null = automatico: Festivo se il giorno è festivo, altrimenti Lavoro.
    /// Impostare Lavoro su un festivo serve a "togliere" il festivo.
    /// </summary>
    public DayType? Type { get; set; }

    public string? Note { get; set; }

    public TimeSpan Worked => Slots.Aggregate(TimeSpan.Zero, (sum, s) => sum + s.Duration);

    /// <summary>Errori di inserimento (fasce invertite o sovrapposte). Vuoto se tutto ok.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
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
}
