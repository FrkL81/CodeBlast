using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBlast.Core.Services;

public class BinaryDetector
{
    private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".bmp", ".svg",
        ".pdf", ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
        ".mp4", ".mp3", ".avi", ".mov", ".wmv", ".flv", ".wav",
        ".woff", ".woff2", ".ttf", ".eot", ".otf",
        ".class", ".pyc", ".o", ".obj", ".lib", ".a", ".so",
        ".doc", ".xls", ".xlsx", ".ppt", ".pptx",
        ".psd", ".ai", ".sketch", ".fig",
        ".db", ".sqlite", ".sqlite3", ".mdb", ".accdb",
        ".iso", ".img", ".bin", ".dat",
        ".dll", ".sys", ".drv",
        ".swf", ".fla"
    };

    public static bool IsBinaryByExtension(string path)
    {
        var extension = System.IO.Path.GetExtension(path);
        return BinaryExtensions.Contains(extension);
    }

    public static bool IsBinary(string path, int bytesToCheck = 8192)
    {
        if (IsBinaryByExtension(path))
            return true;

        try
        {
            if (!System.IO.File.Exists(path)) return false;
            
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
            var buffer = new byte[Math.Min(bytesToCheck, (int)stream.Length)];
            int read = stream.Read(buffer, 0, buffer.Length);

            return buffer.Take(read).Any(b => b == 0);
        }
        catch
        {
            return true;
        }
    }
}
