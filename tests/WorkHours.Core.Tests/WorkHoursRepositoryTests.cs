using WorkHours.Core.Models;
using WorkHours.Core.Storage;

namespace WorkHours.Core.Tests;

public class WorkHoursRepositoryTests
{
    private sealed class MemoryStore : IKeyValueStore
    {
        public Dictionary<string, string> Items { get; } = [];

        public Task<string?> GetAsync(string key) => Task.FromResult(Items.GetValueOrDefault(key));

        public Task SetAsync(string key, string value)
        {
            Items[key] = value;
            return Task.CompletedTask;
        }
    }

    private static TimeSlot Slot(int h1, int h2) => new(new TimeOnly(h1, 0), new TimeOnly(h2, 0));

    private static async Task<WorkHoursRepository> Open(MemoryStore store)
    {
        var repo = new WorkHoursRepository(store);
        await repo.LoadAsync();
        return repo;
    }

    [Fact]
    public async Task GiornoSalvato_SiRitrovaRiaprendoLApp()
    {
        var store = new MemoryStore();
        var repo = await Open(store);
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(13, 15), Slot(9, 10)], Note = " ok " });

        var reopened = await Open(store);
        var day = reopened.GetDay(new(2026, 10, 20));
        Assert.NotNull(day);
        Assert.Equal(TimeSpan.FromHours(3), day.Worked);
        Assert.Equal(new TimeOnly(9, 0), day.Slots[0].Start); // fasce ordinate
        Assert.Equal("ok", day.Note);
    }

    [Fact]
    public async Task GiornoNonValido_NonVieneSalvato()
    {
        var store = new MemoryStore();
        var repo = await Open(store);
        var errors = await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(9, 11), Slot(10, 12)] });

        Assert.NotEmpty(errors);
        Assert.Null(repo.GetDay(new(2026, 10, 20)));
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task GiornoSvuotato_VieneEliminato()
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(9, 10)] });
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20) });
        Assert.Empty(repo.Days);
    }

    [Fact]
    public async Task FestivoTolto_ResistaAlSalvataggio()
    {
        var store = new MemoryStore();
        var repo = await Open(store);
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 12, 8), Type = DayType.Lavoro });

        var reopened = await Open(store);
        Assert.Equal(DayType.Lavoro, reopened.CreateCalculator().EffectiveType(new(2026, 12, 8)));
    }

    [Fact]
    public async Task ExportEImport_RiportanoTuttoUgualeSuUnAltroTelefono()
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(9, 13)], Type = DayType.Lavoro });
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 21), Type = DayType.Ferie });
        var settings = new Settings();
        settings.Schedules.Add(WorkSchedule.Default with { ValidFrom = new(2027, 1, 1), WeeklyHours = 25 });
        await repo.SaveSettingsAsync(settings);

        var json = repo.ExportJson();
        var other = await Open(new MemoryStore());
        await other.ImportJsonAsync(json);

        Assert.Equal(2, other.Days.Count);
        Assert.Equal(DayType.Ferie, other.GetDay(new(2026, 10, 21))!.Type);
        Assert.Equal(2, other.Settings.Schedules.Count);
        Assert.Contains(DayOfWeek.Saturday, other.Settings.Schedules[1].WorkingDays);
        Assert.Equal(json, other.ExportJson());
    }

    [Theory]
    [InlineData("non è json")]
    [InlineData("""{ "version": 99, "settings": { "schedules": [] }, "days": [] }""")]
    [InlineData("""{ "version": 1, "settings": { "schedules": [] }, "days": [] }""")]
    public async Task ImportNonValido_NonToccaIDati(string json)
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(9, 13)] });

        await Assert.ThrowsAsync<InvalidBackupException>(() => repo.ImportJsonAsync(json));
        Assert.Single(repo.Days);
    }

    [Fact]
    public async Task PromemoriaBackup_DopoQuattordiciGiorni()
    {
        var store = new MemoryStore();
        var repo = await Open(store);
        var now = new DateTime(2026, 10, 20, 18, 0, 0);
        var maxAge = TimeSpan.FromDays(14);

        Assert.False(repo.IsBackupDue(now, maxAge)); // nessun dato, niente promemoria
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(9, 13)] });
        Assert.True(repo.IsBackupDue(now, maxAge)); // mai fatto

        await repo.MarkBackupDoneAsync(now);
        var reopened = await Open(store);
        Assert.False(reopened.IsBackupDue(now.AddDays(14), maxAge));
        Assert.True(reopened.IsBackupDue(now.AddDays(15), maxAge));
    }

    [Fact]
    public async Task DatiIlleggibili_VengonoMessiDaParteNonCancellati()
    {
        var store = new MemoryStore();
        store.Items[WorkHoursRepository.StorageKey] = "{ rotto";

        var repo = await Open(store);

        Assert.NotNull(repo.LoadWarning);
        Assert.Empty(repo.Days);
        Assert.Contains(store.Items, kv => kv.Key.StartsWith(WorkHoursRepository.CorruptKeyPrefix) && kv.Value == "{ rotto");
    }
}
