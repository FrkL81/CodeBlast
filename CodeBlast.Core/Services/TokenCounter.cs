using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TiktokenSharp;

namespace CodeBlast.Core.Services;

public class TokenCounter
{
    private static readonly HashSet<string> CommonExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".vb", ".fs", ".js", ".ts", ".jsx", ".tsx", ".lat",
        ".py", ".java", ".cpp", ".c", ".h", ".hpp",
        ".go", ".rs", ".swift", ".kt", ".scala",
        ".rb", ".php", ".pl", ".sh", ".sql",
        ".html", ".css", ".scss", ".less", ".xml", ".json", ".yaml", ".yml", ".toml", ".ini",
        ".md", ".txt", ".rtf", ".docx", ".odt", ".idml",
        ".ps1", ".bat", ".cmd", ".psm1"
    };

    private static readonly TikToken _tikToken;

    static TokenCounter()
    {
        try
        {
            _tikToken = TikToken.GetEncoding("cl100k_base");
        }
        catch
        {
            // Fallback de seguridad
            _tikToken = TikToken.EncodingForModel("gpt-4");
        }
    }

    public static int CountTokens(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        try
        {
            return _tikToken.Encode(content).Count;
        }
        catch
        {
            // Fallback extremo si falla la librería
            var wordCount = content.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Max(1, wordCount * 4 / 3);
        }
    }

    public static async Task<int> CountTokensAsync(string content)
    {
        return await Task.Run(() => CountTokens(content));
    }

    public static bool IsTextFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path);
        return CommonExtensions.Contains(ext) || string.IsNullOrEmpty(ext);
    }
}
