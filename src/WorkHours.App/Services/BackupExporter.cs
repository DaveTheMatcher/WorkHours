using Microsoft.JSInterop;
using WorkHours.Core.Storage;

namespace WorkHours.App.Services;

/// <summary>Esporta il backup (menu Condividi su iPhone) e ricorda quando è stato fatto.</summary>
public sealed class BackupExporter(WorkHoursRepository repo, IJSRuntime js)
{
    public static readonly TimeSpan ReminderAfter = TimeSpan.FromDays(14);

    public async Task ExportAsync()
    {
        var fileName = $"workhours-backup-{DateTime.Now:yyyy-MM-dd}.json";
        var done = await js.InvokeAsync<bool>("workHours.saveFile", fileName, repo.ExportJson());
        if (done)
            await repo.MarkBackupDoneAsync(DateTime.Now);
    }
}
