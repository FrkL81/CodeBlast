using System;
using System.IO;
using System.Threading.Tasks;

namespace CodeBlast.Core.Services;

public class ExportService
{
    public async Task ExportToFileAsync(string content, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, content);
    }

    public string GenerateFileName(string projectName, string extension)
    {
        var date = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var safeProjectName = new string(projectName.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        return $"{safeProjectName}_{date}.{extension}";
    }
}
