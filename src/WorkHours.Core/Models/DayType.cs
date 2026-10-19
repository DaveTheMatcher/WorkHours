namespace WorkHours.Core.Models;

public enum DayType
{
    /// <summary>Giorno normale: vale le ore giornaliere del monte ore.</summary>
    Lavoro,
    Ferie,
    Malattia,
    Permesso,
    /// <summary>Festivo: toglie il giorno dall'obiettivo.</summary>
    Festivo,
}

public static class DayTypeExtensions
{
    /// <summary>Tutti i tipi tranne Lavoro "coprono" le ore del giorno (non contano nell'obiettivo).</summary>
    public static bool CoversTarget(this DayType type) => type != DayType.Lavoro;
}
