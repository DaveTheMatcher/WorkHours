namespace WorkHours.Core.Storage;

/// <summary>Archivio chiave/valore. Nell'app è il localStorage del browser, nei test è in memoria.</summary>
public interface IKeyValueStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);
}
