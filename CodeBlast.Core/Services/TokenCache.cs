using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeBlast.Core.Services;

public class TokenCache
{
    private string _cachePath = string.Empty;
    private Dictionary<string, CacheEntry> _entries = new();

    public record CacheEntry(long SizeBytes, DateTime LastWrite, int TokenCount);

    public void Initialize(string projectPath)
    {
        _cachePath = Path.Combine(projectPath, ".codeblast-cache.json");
        _entries.Clear(); // NUEVO: Limpiar memoria residual de proyectos anteriores
    }

    public int? TryGet(string fullPath)
    {
        if (string.IsNullOrEmpty(_cachePath)) return null;

        var info = new FileInfo(fullPath);
        if (!info.Exists) return null;

        if (_entries.TryGetValue(fullPath, out var entry)
            && entry.SizeBytes == info.Length
            && Math.Abs((entry.LastWrite - info.LastWriteTimeUtc).TotalSeconds) < 1)
        {
            return entry.TokenCount;
        }
        return null;
    }

    public void Set(string fullPath, int tokenCount)
    {
        if (string.IsNullOrEmpty(_cachePath)) return;

        var info = new FileInfo(fullPath);
        if (!info.Exists) return;

        _entries[fullPath] = new CacheEntry(info.Length, info.LastWriteTimeUtc, tokenCount);
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(_cachePath)) return;

        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_entries, options);
            await File.WriteAllTextAsync(_cachePath, json);
        }
        catch { /* Ignorar errores de guardado */ }
    }

    public async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(_cachePath)) return;

        if (!File.Exists(_cachePath))
        {
            _entries.Clear(); // NUEVO: Si no hay caché previo, asegurar inicio limpio
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_cachePath);
            _entries = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json) ?? new();
        }
        catch 
        { 
            _entries.Clear(); // MODIFICADO: Usar Clear en lugar de instanciar de nuevo
        }
    }
}
