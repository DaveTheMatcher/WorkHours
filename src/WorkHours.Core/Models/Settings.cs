namespace WorkHours.Core.Models;

/// <summary>Monte ore valido da una certa data in poi.</summary>
public sealed record WorkSchedule(DateOnly ValidFrom, double WeeklyHours, HashSet<DayOfWeek> WorkingDays)
{
    public TimeSpan DailyHours => WorkingDays.Count == 0
        ? TimeSpan.Zero
        : TimeSpan.FromHours(WeeklyHours / WorkingDays.Count);

    public bool IsWorkingDay(DateOnly date) => WorkingDays.Contains(date.DayOfWeek);

    /// <summary>20 h a settimana, da martedì a sabato.</summary>
    public static WorkSchedule Default => new(
        DateOnly.MinValue,
        20,
        new HashSet<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday });
}

public sealed class Settings
{
    /// <summary>Storico dei monte ore: se cambia, i mesi passati restano calcolati con quello vecchio.</summary>
    public List<WorkSchedule> Schedules { get; set; } = [WorkSchedule.Default];

    public WorkSchedule ScheduleFor(DateOnly date) =>
        Schedules.Where(s => s.ValidFrom <= date).MaxBy(s => s.ValidFrom) ?? WorkSchedule.Default;
}
