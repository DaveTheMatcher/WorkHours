using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using WorkHours.App;
using WorkHours.App.Services;
using WorkHours.Core.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<IKeyValueStore, BrowserStorage>();
builder.Services.AddSingleton<WorkHoursRepository>();
builder.Services.AddSingleton<BackupExporter>();

var host = builder.Build();

// Carica i dati una volta sola all'avvio, prima di mostrare le pagine.
await host.Services.GetRequiredService<WorkHoursRepository>().LoadAsync();
await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<bool>("workHours.requestPersistentStorage");

await host.RunAsync();
