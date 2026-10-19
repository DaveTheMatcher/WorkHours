namespace WorkHours.Core.Models;

/// <summary>Una fascia oraria lavorata, es. 9:00–10:30.</summary>
public sealed record TimeSlot(TimeOnly Start, TimeOnly End)
{
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;

    public bool IsValid => End > Start;

    public bool Overlaps(TimeSlot other) => Start < other.End && other.Start < End;
}
