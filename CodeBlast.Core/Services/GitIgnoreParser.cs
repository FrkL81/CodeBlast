using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Globbing;

namespace CodeBlast.Core.Services;

public class GitIgnoreParser
{
    private readonly List<IgnoreRule> _rules = new();

    private record IgnoreRule(Glob GlobPattern, bool IsNegation, bool IsDirectoryOnly);

    public async Task LoadFromDirectoryAsync(string rootPath)
    {
        _rules.Clear();
        var ignoreFiles = new List<string>();

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
        };

        ignoreFiles.AddRange(Directory.EnumerateFiles(rootPath, ".gitignore", options));
        ignoreFiles.AddRange(Directory.EnumerateFiles(rootPath, ".codeblastignore", options));

        var tasks = ignoreFiles.Select(async file => 
        {
            var basePath = Path.GetDirectoryName(file)!;
            var lines = await File.ReadAllLinesAsync(file);
            return (basePath, lines);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var (basePath, lines) in results)
        {
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                
                var isNegation = trimmed.StartsWith('!');
                var rawPattern = isNegation ? trimmed[1..] : trimmed;
                
                // Normalizar backslashes (Windows) a forward slashes
                rawPattern = rawPattern.Replace('\\', '/');
                var isDirectoryOnly = rawPattern.EndsWith('/');
                if (isDirectoryOnly) rawPattern = rawPattern.TrimEnd('/');

                // Generar el patrón Glob
                string globString;
                if (rawPattern.StartsWith('/'))
                {
                    // Relativo a la ubicación del archivo ignore
                    globString = rawPattern[1..];
                }
                else if (!rawPattern.Contains('/'))
                {
                    // Aplica en cualquier subdirectorio
                    globString = "**/" + rawPattern;
                }
                else
                {
                    globString = rawPattern;
                }

                // Ajustar ruta relativa a la raíz del proyecto para el Glob
                var relativeBase = GetRelativePath(rootPath, basePath).Replace('\\', '/');
                if (!string.IsNullOrEmpty(relativeBase))
                {
                    globString = relativeBase + "/" + globString;
                }

                try 
                {
                    var glob = Glob.Parse(globString);
                    _rules.Add(new IgnoreRule(glob, isNegation, isDirectoryOnly));
                }
                catch { /* Ignorar patrones malformados */ }
            }
        }
    }

    public bool IsIgnored(string fullPath, string relativePath, bool isDirectory)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        bool ignored = false;

        foreach (var rule in _rules)
        {
            if (rule.IsDirectoryOnly && !isDirectory) continue;

            if (rule.GlobPattern.IsMatch(normalizedPath))
            {
                ignored = !rule.IsNegation;
            }
        }

        return ignored;
    }

    private static string GetRelativePath(string rootPath, string fullPath)
    {
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)) return fullPath;
        return fullPath[rootPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
