using Microsoft.AspNetCore.Components;
using WorkHours.Core.Storage;

namespace WorkHours.App.Components;

/// <summary>Base per le pagine che si ridisegnano quando i dati cambiano (salvataggio, import, backup).</summary>
public abstract class RefreshOnChange : ComponentBase, IDisposable
{
    [Inject] protected WorkHoursRepository Repo { get; set; } = default!;

    protected override void OnInitialized() => Repo.Changed += OnDataChanged;

    private void OnDataChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => Repo.Changed -= OnDataChanged;
}
