using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CodeBlast.Core.Services;

public static class FileContentReader
{
    public static async Task<string> ReadTextAsync(string filePath, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".docx" => await Task.Run(() => ReadZippedXml(filePath, "word/document.xml", "p"), ct),
            ".odt" => await Task.Run(() => ReadZippedXml(filePath, "content.xml", "p"), ct),
            ".idml" => await Task.Run(() => ReadIdmlStories(filePath), ct),
            _ => await File.ReadAllTextAsync(filePath, ct)
        };
    }

    private static string ReadZippedXml(string zipPath, string xmlEntryName, string paragraphNodeName)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(xmlEntryName);
            if (entry == null) return string.Empty;

            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true });
            
            var sb = new StringBuilder();
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == paragraphNodeName)
                {
                    sb.AppendLine(); // Nuevo párrafo
                }
                else if (reader.NodeType == XmlNodeType.Text)
                {
                    sb.Append(reader.Value);
                }
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return $"[Error extrayendo texto del archivo {Path.GetFileName(zipPath)}]";
        }
    }

    private static string ReadIdmlStories(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var sb = new StringBuilder();

            // InDesign divide el texto en "Stories"
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith("Stories/Story_") && entry.FullName.EndsWith(".xml"))
                {
                    using var stream = entry.Open();
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreComments = true });
                    
                    bool inContent = false;
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Content")
                        {
                            inContent = true;
                        }
                        else if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "Content")
                        {
                            inContent = false;
                            sb.AppendLine(); // Salto de línea al terminar un bloque de contenido
                        }
                        else if (inContent && reader.NodeType == XmlNodeType.Text)
                        {
                            sb.Append(reader.Value);
                        }
                    }
                    sb.AppendLine(); // Separación visual entre distintas Stories
                }
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return $"[Error extrayendo texto del archivo IDML {Path.GetFileName(zipPath)}]";
        }
    }
}
