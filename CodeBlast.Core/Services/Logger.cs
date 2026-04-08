using System;
using System.IO;
using System.Threading.Tasks;

namespace CodeBlast.Core.Services;

public static class Logger
{
    private static readonly string LogFile;
    private static bool _isEnabled = false;
    private static readonly object _lock = new();

    public static bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    static Logger()
    {
        string root = AppDomain.CurrentDomain.BaseDirectory;
        try
        {
            // Intentar encontrar la raíz del proyecto buscando el .sln o .slnx hacia arriba
            var dir = new DirectoryInfo(root);
            while (dir != null && !dir.GetFiles("*.sln").Any() && !dir.GetFiles("*.slnx").Any())
            {
                dir = dir.Parent;
            }
            if (dir != null) root = dir.FullName;
            else root = Directory.GetCurrentDirectory(); // Fallback
        }
        catch { root = Directory.GetCurrentDirectory(); }

        var logFolder = Path.Combine(root, "logs");
        LogFile = Path.Combine(logFolder, $"log_{DateTime.Now:yyyyMMdd}.txt");

        try
        {
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
        }
        catch { }
    }

    public static void Log(string message, string level = "INFO")
    {
        if (!_isEnabled) return;

        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(LogFile, logLine);
            }
        }
        catch { }
    }

    public static void LogError(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} - EXCEPTION: {ex}" : message;
        Log(msg, "ERROR");
    }

    public static async Task LogAsync(string message, string level = "INFO")
    {
        if (!_isEnabled) return;

        try
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(LogFile, logLine);
        }
        catch { }
    }
}
