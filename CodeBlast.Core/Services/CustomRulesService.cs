using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNet.Globbing;

namespace CodeBlast.Core.Services;

public class CustomRulesService
{
    private readonly List<string> _rawRules = new();
    private readonly List<Glob> _parsedRules = new();

    public IReadOnlyList<string> Rules => _rawRules.AsReadOnly();

    public void AddRule(string pattern)
    {
        if (!_rawRules.Contains(pattern))
        {
            var rules = _rawRules.ToList();
            rules.Add(pattern);
            SetRules(rules);
        }
    }

    public void RemoveRule(string pattern)
    {
        if (_rawRules.Contains(pattern))
        {
            var rules = _rawRules.ToList();
            rules.Remove(pattern);
            SetRules(rules);
        }
    }

    public void SetRules(IEnumerable<string> newRules)
    {
        _rawRules.Clear();
        _parsedRules.Clear();

        foreach (var rule in newRules)
        {
            if (string.IsNullOrWhiteSpace(rule)) continue;
            
            _rawRules.Add(rule);

            // Normalizar para DotNet.Glob
            var pattern = rule.Trim().Replace('\\', '/');
            
            // Si es un patrón simple sin slashes (ej. "*.min.js" o "bin"), aplicarlo recursivamente
            if (!pattern.Contains('/'))
            {
                pattern = "**/" + pattern;
            }
            // Si es un directorio explícito (ej. "node_modules/"), asegurar que atrape el contenido
            else if (pattern.EndsWith('/'))
            {
                pattern += "**";
            }

            try
            {
                _parsedRules.Add(Glob.Parse(pattern));
            }
            catch { /* Ignorar reglas malformadas */ }
        }
    }

    public bool IsExcluded(string relativePath, bool isDirectory)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        
        foreach (var glob in _parsedRules)
        {
            if (glob.IsMatch(normalizedPath))
            {
                return true;
            }
        }
        return false;
    }

    public void SaveToFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllLines(path, _rawRules);
    }

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        var rules = File.ReadAllLines(path);
        SetRules(rules);
    }
}
