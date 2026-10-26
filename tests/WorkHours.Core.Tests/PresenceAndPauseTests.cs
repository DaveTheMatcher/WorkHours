using WorkHours.Core.Models;
using WorkHours.Core.Services;
using WorkHours.Core.Storage;

namespace WorkHours.Core.Tests;

/// <summary>Inserimento a presenza e pausa, e convivenza con i giorni già inseriti a fasce.</summary>
public class PresenceAndPauseTests
{
    private static TimeSpan T(int h, int m = 0) => new(h, m, 0);

    private static TimeSlot Slot(int h1, int m1, int h2, int m2) => new(new TimeOnly(h1, m1), new TimeOnly(h2, m2));

    // ---- Calcolo del giorno ----

    [Fact]
    public void Lavorato_EPresenzaMenoPausa()
    {
        var day = new WorkDay { Presence = T(5, 30), Pause = T(1, 20) };
        Assert.Equal(T(4, 10), day.Worked);
        Assert.Empty(day.Validate());
        Assert.False(day.UsesSlots);
    }

    [Fact]
    public void SenzaPausa_ContaTuttaLaPresenza()
    {
        var day = new WorkDay { Presence = T(4) };
        Assert.Equal(T(4), day.Worked);
        Assert.Empty(day.Validate());
    }

    [Fact]
    public void PausaUgualeAllaPresenza_ZeroOreMaValido()
    {
        var day = new WorkDay { Presence = T(1), Pause = T(1) };
        Assert.Equal(TimeSpan.Zero, day.Worked);
        Assert.Empty(day.Validate());
    }

    [Fact]
    public void GiornoAFasce_SiCalcolaComePrima()
    {
        var day = new WorkDay { Slots = [Slot(10, 0, 11, 0), Slot(13, 0, 15, 0)] };
        Assert.Equal(T(3), day.Worked);
        Assert.True(day.UsesSlots);
    }

    // ---- Validazione ----

    [Fact]
    public void PausaPiuLungaDellaPresenza_DaErrore()
    {
        var day = new WorkDay { Presence = T(2), Pause = T(2, 1) };
        Assert.Single(day.Validate());
        Assert.Equal(TimeSpan.Zero, day.Worked); // mai negativo, neanche se arrivasse così
    }

    [Fact]
    public void PresenzaOltre24Ore_DaErrore()
    {
        Assert.Single(new WorkDay { Presence = T(24, 1) }.Validate());
        Assert.Empty(new WorkDay { Presence = T(24) }.Validate());
    }

    [Fact]
    public void ValoriNegativi_DannoErrore()
    {
        Assert.NotEmpty(new WorkDay { Presence = T(-1) }.Validate());
        Assert.NotEmpty(new WorkDay { Presence = T(4), Pause = T(-1) }.Validate());
    }

    [Fact]
    public void FasceEPresenzaInsieme_DaErrore()
    {
        var day = new WorkDay { Slots = [Slot(9, 0, 10, 0)], Presence = T(4) };
        Assert.Single(day.Validate());
    }

    [Fact]
    public void PausaSenzaPresenza_DaErrore()
    {
        Assert.Single(new WorkDay { Pause = T(1) }.Validate());
    }

    // ---- Conversione dalle fasce ----

    [Fact]
    public void Conversione_EsempioDueFasce()
    {
        var day = new WorkDay { Slots = [Slot(10, 0, 11, 0), Slot(13, 0, 15, 0)] };
        Assert.Equal((T(5), T(2)), day.ToPresenceAndPause());
    }

    [Fact]
    public void Conversione_UnaFasciaNonHaPausa()
    {
        var day = new WorkDay { Slots = [Slot(9, 0, 13, 0)] };
        Assert.Equal((T(4), TimeSpan.Zero), day.ToPresenceAndPause());
    }

    [Fact]
    public void Conversione_GiornoVuoto()
    {
        Assert.Equal((TimeSpan.Zero, TimeSpan.Zero), new WorkDay().ToPresenceAndPause());
    }

    [Fact]
    public void Conversione_GiornoGiaNuovoRestituisceISuoiValori()
    {
        var day = new WorkDay { Presence = T(5, 30), Pause = T(1, 20) };
        Assert.Equal((T(5, 30), T(1, 20)), day.ToPresenceAndPause());
        Assert.Equal((T(4), TimeSpan.Zero), new WorkDay { Presence = T(4) }.ToPresenceAndPause());
    }

    public static TheoryData<TimeSlot[]> CasiDiFasce => new()
    {
        new[] { Slot(9, 0, 10, 0) },
        new[] { Slot(10, 0, 11, 0), Slot(13, 0, 15, 0) },
        new[] { Slot(9, 0, 10, 0), Slot(11, 0, 12, 30), Slot(13, 30, 15, 0) },
        new[] { Slot(13, 30, 15, 0), Slot(9, 15, 10, 45) }, // fasce non in ordine
        new[] { Slot(9, 0, 12, 0), Slot(12, 0, 14, 0) },     // fasce attaccate: nessuna pausa
        new[] { Slot(8, 7, 9, 53), Slot(14, 11, 19, 59) },  // minuti "storti"
    };

    [Theory]
    [MemberData(nameof(CasiDiFasce))]
    public void Conversione_NonCambiaMaiLeOreLavorate(TimeSlot[] slots)
    {
        var old = new WorkDay { Slots = [.. slots] };
        var (presence, pause) = old.ToPresenceAndPause();
        var converted = new WorkDay { Presence = presence, Pause = pause };

        Assert.Empty(converted.Validate());
        Assert.Equal(old.Worked, converted.Worked);
    }

    // ---- Saldo con giorni dei due tipi ----

    [Fact]
    public void Saldo_MeseConGiorniVecchiENuovi()
    {
        // Ottobre 2026, prima registrazione martedì 6: previste 3 giornate da 4 h = 12 h.
        var days = new List<WorkDay>
        {
            new() { Date = new(2026, 10, 6), Slots = [Slot(10, 0, 11, 0), Slot(13, 0, 16, 0)] }, // 4h a fasce
            new() { Date = new(2026, 10, 7), Presence = T(5, 30), Pause = T(1, 20) },           // 4h10
            new() { Date = new(2026, 10, 8), Presence = T(4), Pause = T(0, 30) },               // 3h30
        };
        var calc = new BalanceCalculator(new Settings(), days);
        var month = calc.Month(2026, 10, new DateOnly(2026, 10, 8));

        Assert.Equal(T(11, 40), month.Worked);
        Assert.Equal(T(12), month.ExpectedToDate);
        Assert.Equal(-T(0, 20), month.Balance);
        Assert.Equal(T(4, 10), calc.Worked(new DateOnly(2026, 10, 7)));
    }

    // ---- Salvataggio e lettura ----

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

    private static async Task<WorkHoursRepository> Open(MemoryStore store)
    {
        var repo = new WorkHoursRepository(store);
        await repo.LoadAsync();
        return repo;
    }

    /// <summary>Dati come li ha salvati la prima versione dell'app (formato 1, solo fasce).</summary>
    private const string DatiVersione1 = """
        {
          "version": 1,
          "settings": {
            "schedules": [
              {
                "validFrom": "0001-01-01",
                "weeklyHours": 20,
                "workingDays": [ "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" ]
              }
            ]
          },
          "days": [
            {
              "date": "2026-10-20",
              "slots": [
                { "start": "10:00:00", "end": "11:00:00" },
                { "start": "13:00:00", "end": "15:00:00" }
              ],
              "note": "prima settimana"
            },
            {
              "date": "2026-10-21",
              "slots": [],
              "type": "Ferie"
            }
          ]
        }
        """;

    [Fact]
    public async Task DatiGiaSulTelefono_SiLeggonoComePrima()
    {
        var store = new MemoryStore();
        store.Items[WorkHoursRepository.StorageKey] = DatiVersione1;

        var repo = await Open(store);

        Assert.Null(repo.LoadWarning);
        Assert.Equal(2, repo.Days.Count);
        var day = repo.GetDay(new(2026, 10, 20))!;
        Assert.True(day.UsesSlots);
        Assert.Equal(T(3), day.Worked);
        Assert.Equal("prima settimana", day.Note);
        Assert.Equal(DayType.Ferie, repo.GetDay(new(2026, 10, 21))!.Type);
    }

    [Fact]
    public async Task DatiGiaSulTelefono_DopoUnNuovoSalvataggioRestanoAFasce()
    {
        var store = new MemoryStore();
        store.Items[WorkHoursRepository.StorageKey] = DatiVersione1;
        var repo = await Open(store);

        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 22), Presence = T(5, 30), Pause = T(1, 20) });

        var json = store.Items[WorkHoursRepository.StorageKey];
        Assert.Contains("\"version\": 2", json);

        var reopened = await Open(store);
        Assert.Equal(3, reopened.Days.Count);
        Assert.True(reopened.GetDay(new(2026, 10, 20))!.UsesSlots);
        Assert.Equal(T(3), reopened.GetDay(new(2026, 10, 20))!.Worked);
        Assert.Equal(T(4, 10), reopened.GetDay(new(2026, 10, 22))!.Worked);
    }

    [Fact]
    public async Task BackupVersione1_SiImporta()
    {
        var repo = await Open(new MemoryStore());
        await repo.ImportJsonAsync(DatiVersione1);

        Assert.Equal(2, repo.Days.Count);
        Assert.Equal(T(3), repo.GetDay(new(2026, 10, 20))!.Worked);
    }

    [Fact]
    public async Task BackupDiVersioneFutura_Viene_Rifiutato()
    {
        var repo = await Open(new MemoryStore());
        var json = DatiVersione1.Replace("\"version\": 1", "\"version\": 3");

        await Assert.ThrowsAsync<InvalidBackupException>(() => repo.ImportJsonAsync(json));
        Assert.Empty(repo.Days);
    }

    [Fact]
    public async Task GiornoNuovo_SiRitrovaRiaprendoLApp()
    {
        var store = new MemoryStore();
        var repo = await Open(store);
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Presence = T(5, 30), Pause = T(1, 20) });

        var day = (await Open(store)).GetDay(new(2026, 10, 20))!;
        Assert.Equal(T(5, 30), day.Presence);
        Assert.Equal(T(1, 20), day.Pause);
        Assert.Empty(day.Slots);
        Assert.Equal(T(4, 10), day.Worked);
    }

    [Fact]
    public async Task GiornoVecchioModificato_PassaAlFormatoNuovoConLeStesseOre()
    {
        var store = new MemoryStore();
        store.Items[WorkHoursRepository.StorageKey] = DatiVersione1;
        var repo = await Open(store);

        // Quello che farà l'editor: legge le fasce già convertite e salva presenza e pausa.
        var old = repo.GetDay(new(2026, 10, 20))!;
        var (presence, pause) = old.ToPresenceAndPause();
        await repo.SaveDayAsync(new WorkDay { Date = old.Date, Presence = presence, Pause = pause, Note = old.Note });

        var day = (await Open(store)).GetDay(new(2026, 10, 20))!;
        Assert.False(day.UsesSlots);
        Assert.Empty(day.Slots);
        Assert.Equal(T(5), day.Presence);
        Assert.Equal(T(2), day.Pause);
        Assert.Equal(T(3), day.Worked);
        Assert.Equal("prima settimana", day.Note);
    }

    [Fact]
    public async Task PausaAZero_NonVieneSalvata()
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Presence = T(4), Pause = TimeSpan.Zero });

        Assert.Null(repo.GetDay(new(2026, 10, 20))!.Pause);
        Assert.DoesNotContain("pause", repo.ExportJson());
    }

    [Fact]
    public async Task PresenzaEPausaAZero_EliminanoIlGiorno()
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Presence = T(4) });
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Presence = TimeSpan.Zero, Pause = TimeSpan.Zero });

        Assert.Empty(repo.Days);
    }

    [Fact]
    public async Task PresenzaAZeroConNota_TieneSoloLaNota()
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Presence = TimeSpan.Zero, Note = "chiuso per lavori" });

        var day = repo.GetDay(new(2026, 10, 20))!;
        Assert.Null(day.Presence);
        Assert.Equal(TimeSpan.Zero, day.Worked);
    }

    [Fact]
    public async Task PausaTroppoLunga_NonVieneSalvata()
    {
        var store = new MemoryStore();
        var repo = await Open(store);
        var errors = await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Presence = T(2), Pause = T(3) });

        Assert.NotEmpty(errors);
        Assert.Empty(repo.Days);
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task ExportEImport_ConGiorniDeiDueTipi()
    {
        var repo = await Open(new MemoryStore());
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 20), Slots = [Slot(10, 0, 11, 0), Slot(13, 0, 15, 0)] });
        await repo.SaveDayAsync(new WorkDay { Date = new(2026, 10, 21), Presence = T(5, 30), Pause = T(1, 20) });

        var json = repo.ExportJson();
        var other = await Open(new MemoryStore());
        await other.ImportJsonAsync(json);

        Assert.Equal(T(3), other.GetDay(new(2026, 10, 20))!.Worked);
        Assert.Equal(T(4, 10), other.GetDay(new(2026, 10, 21))!.Worked);
        Assert.Equal(json, other.ExportJson());
    }

    [Fact]
    public async Task ImportConPausaPiuLungaDellaPresenza_Viene_Rifiutato()
    {
        var repo = await Open(new MemoryStore());
        var json = DatiVersione1
            .Replace("\"version\": 1", "\"version\": 2")
            .Replace("\"slots\": [],", "\"presence\": \"01:00:00\", \"pause\": \"02:00:00\",");

        await Assert.ThrowsAsync<InvalidBackupException>(() => repo.ImportJsonAsync(json));
    }
}
