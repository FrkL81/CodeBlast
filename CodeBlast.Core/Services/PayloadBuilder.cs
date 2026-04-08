using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBlast.Core.Models;

namespace CodeBlast.Core.Services;

public class PayloadBuilder
{
    private readonly OutputMode _outputMode;
    private readonly string _projectName;

    public PayloadBuilder(OutputMode outputMode, string projectName)
    {
        _outputMode = outputMode;
        _projectName = projectName;
    }

    public async Task<string> BuildAsync(IEnumerable<FileNode> selectedNodes, CancellationToken ct = default)
    {
        // El mapa incluye todos los archivos seleccionados (incluso binarios), excluyendo solo directorios y excluidos
        var mapNodes = selectedNodes.Where(n => !n.IsExcluded && !n.IsDirectory).ToList();
        // El contenido excluye a los binarios
        var contentNodes = mapNodes.Where(n => !n.IsBinary).ToList();

        if (_outputMode == OutputMode.FileMapOnly)
        {
            return BuildFileMap(mapNodes);
        }

        var content = await BuildContentAsync(contentNodes, ct);

        if (_outputMode == OutputMode.ContentOnly)
        {
            return content;
        }

        return BuildFileMap(mapNodes) + "\n\n" + content;
    }

    public string BuildFileMap(IEnumerable<FileNode> nodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Project: {_projectName}");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"{_projectName}\\");

        // Build a tree structure from flat relative paths
        var nodeList = nodes.ToList();
        
        // Use a case-insensitive dictionary to grouping by directory
        var root = new TreeEntry(_projectName);

        foreach (var node in nodeList)
        {
            var parts = node.RelativePath.Split(Path.DirectorySeparatorChar);
            var current = root;
            for (int i = 0; i < parts.Length - 1; i++)
                current = current.GetOrAddChild(parts[i], isDir: true);
            current.GetOrAddChild(parts[^1], isDir: false);
        }

        RenderTree(root.Children, sb, prefix: "");
        return sb.ToString();
    }

    private static void RenderTree(List<TreeEntry> entries, StringBuilder sb, string prefix)
    {
        var sortedEntries = entries.OrderByDescending(e => e.IsDir).ThenBy(e => e.Name).ToList();
        for (int i = 0; i < sortedEntries.Count; i++)
        {
            var entry = sortedEntries[i];
            bool isLast = i == sortedEntries.Count - 1;
            sb.AppendLine($"{prefix}{(isLast ? "└── " : "├── ")}{entry.Name}{(entry.IsDir ? "\\" : "")}");
            if (entry.Children.Count > 0)
                RenderTree(entry.Children, sb, prefix + (isLast ? "    " : "│   "));
        }
    }

    private class TreeEntry
    {
        public string Name { get; }
        public bool IsDir { get; }
        public List<TreeEntry> Children { get; } = new();

        public TreeEntry(string name, bool isDir = true) { Name = name; IsDir = isDir; }

        public TreeEntry GetOrAddChild(string name, bool isDir)
        {
            var existing = Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase) && c.IsDir == isDir);
            if (existing != null) return existing;
            var entry = new TreeEntry(name, isDir);
            Children.Add(entry);
            return entry;
        }
    }

    private async Task<string> BuildContentAsync(IEnumerable<FileNode> nodes, CancellationToken ct)
    {
        var nodeList = nodes.ToList();
        var results = new string[nodeList.Count];

        await Parallel.ForEachAsync(
            nodeList.Select((node, i) => (node, i)),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
            async (item, innerCt) =>
            {
                try
                {
                    results[item.i] = await FileContentReader.ReadTextAsync(item.node.FullPath, innerCt);
                }
                catch { results[item.i] = string.Empty; }
            });

        var sb = new StringBuilder();
        for (int i = 0; i < nodeList.Count; i++)
        {
            if (!string.IsNullOrEmpty(results[i]))
            {
                sb.AppendLine($"<file path=\"{nodeList[i].RelativePath}\">");
                sb.AppendLine(results[i]);
                sb.AppendLine("</file>");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    public async Task<string> BuildMarkdownAsync(IEnumerable<FileNode> selectedNodes, CancellationToken ct = default)
    {
        var mapNodes = selectedNodes.Where(n => !n.IsExcluded && !n.IsDirectory).ToList();
        var contentNodes = mapNodes.Where(n => !n.IsBinary).ToList();
        var contentResults = new string[contentNodes.Count];

        await Parallel.ForEachAsync(
            contentNodes.Select((node, i) => (node, i)),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
            async (item, innerCt) =>
            {
                try
                {
                    contentResults[item.i] = await FileContentReader.ReadTextAsync(item.node.FullPath, innerCt);
                }
                catch { contentResults[item.i] = string.Empty; }
            });

        var sb = new StringBuilder();
        sb.AppendLine("## File Map");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.Append(BuildFileMap(mapNodes));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Files");

        for (int i = 0; i < contentNodes.Count; i++)
        {
            if (string.IsNullOrEmpty(contentResults[i])) continue;

            var node = contentNodes[i];
            var extension = Path.GetExtension(node.Name).TrimStart('.');
            var languageHint = GetLanguageHint(extension);

            sb.AppendLine();
            sb.AppendLine($"### {node.RelativePath}");
            sb.AppendLine();
            sb.AppendLine($"```{languageHint}");
            sb.AppendLine(contentResults[i]);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private static string GetLanguageHint(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            "cs" => "csharp",
            "vb" => "vb",
            "fs" => "fsharp",
            "js" => "javascript",
            "ts" => "typescript",
            "jsx" => "jsx",
            "tsx" => "tsx",
            "py" => "python",
            "java" => "java",
            "cpp" => "cpp",
            "c" => "c",
            "h" => "c",
            "go" => "go",
            "rs" => "rust",
            "swift" => "swift",
            "kt" => "kotlin",
            "rb" => "ruby",
            "php" => "php",
            "html" => "html",
            "css" => "css",
            "scss" => "scss",
            "xml" => "xml",
            "json" => "json",
            "yaml" => "yaml",
            "yml" => "yaml",
            "md" => "markdown",
            "sh" => "bash",
            "ps1" => "powershell",
            "bat" => "batch",
            "sql" => "sql",
            _ => string.Empty
        };
    }
}
