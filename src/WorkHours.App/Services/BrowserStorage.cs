using Microsoft.JSInterop;
using WorkHours.Core.Storage;

namespace WorkHours.App.Services;

/// <summary>Salva i dati nel localStorage del browser (sul telefono restano sul dispositivo).</summary>
public sealed class BrowserStorage(IJSRuntime js) : IKeyValueStore
{
    public async Task<string?> GetAsync(string key) =>
        await js.InvokeAsync<string?>("localStorage.getItem", key);

    public async Task SetAsync(string key, string value) =>
        await js.InvokeVoidAsync("localStorage.setItem", key, value);
}
