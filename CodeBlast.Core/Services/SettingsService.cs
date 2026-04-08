using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CodeBlast.Core.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var codeBlastPath = Path.Combine(appDataPath, "CodeBlast");
        _settingsPath = Path.Combine(codeBlastPath, "settings.json");

        if (!Directory.Exists(codeBlastPath))
        {
            Directory.CreateDirectory(codeBlastPath);
        }
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
        }
    }

    public string GetProjectStatePath(string projectPath)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var codeBlastPath = Path.Combine(appDataPath, "CodeBlast");
        var projectsPath = Path.Combine(codeBlastPath, "projects");

        var hash = GenerateHash(projectPath);
        var projectDir = Path.Combine(projectsPath, hash);

        if (!Directory.Exists(projectDir))
        {
            Directory.CreateDirectory(projectDir);
        }

        return Path.Combine(projectDir, "state.json");
    }

    public string GetCustomRulesPath(string projectPath)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var codeBlastPath = Path.Combine(appDataPath, "CodeBlast");
        var projectsPath = Path.Combine(codeBlastPath, "projects");

        var hash = GenerateHash(projectPath);
        var projectDir = Path.Combine(projectsPath, hash);

        if (!Directory.Exists(projectDir))
        {
            Directory.CreateDirectory(projectDir);
        }

        return Path.Combine(projectDir, "custom_rules.json");
    }

    private static string GenerateHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..16];
    }
}

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public int TreeFontSize { get; set; } = 13;
    public bool ShowTokensInTree { get; set; } = true;
    public int RecentProjectsCount { get; set; } = 10;
    public bool ExcludeCacheFile { get; set; } = true;
    public List<string> GlobalExclusions { get; set; } = new()
    {
        "node_modules/", ".git/", "bin/", "obj/", "dist/", "build/", ".vs/", 
        "__pycache__/", "*.min.js", "*.min.css", "package-lock.json", 
        "yarn.lock", "*.lock", "*.suo", "*.user", ".DS_Store", "Thumbs.db"
    };
    public string OutputFormat { get; set; } = "XmlLike";
    public string FileSeparator { get; set; } = "EmptyLine";
    public string DefaultOutputMode { get; set; } = "Both";
    public string SelectedModel { get; set; } = "Claude 3.5 / 3.7";
    public int ContextLimit { get; set; } = 200000;
    public double SplitterPosition { get; set; } = 0.35;
}
