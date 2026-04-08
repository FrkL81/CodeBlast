using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBlast.Core.Collections;
using CodeBlast.Core.Models;

namespace CodeBlast.Core.Services;

public class FileSystemScanner
{
    private readonly ExclusionEngine _exclusionEngine;
    private readonly string _rootPath;

    public FileSystemScanner(string rootPath, ExclusionEngine exclusionEngine)
    {
        _rootPath = rootPath;
        _exclusionEngine = exclusionEngine;
    }

    public async Task<BulkObservableCollection<FileNode>> ScanAsync()
    {
        var rootNodes = new BulkObservableCollection<FileNode>();
        // Ejecutar el escaneo real en el ThreadPool
        await Task.Run(() => ScanDirectoryAsync(_rootPath, null, rootNodes));
        return rootNodes;
    }

    private async Task ScanDirectoryAsync(string directoryPath, FileNode? parentNode, BulkObservableCollection<FileNode> nodes)
    {
        try
        {
            Logger.Log($"Scanning directory: {directoryPath}");
            var options = new EnumerationOptions 
            { 
                RecurseSubdirectories = false, 
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint
            };

            var entries = Directory.EnumerateFileSystemEntries(directoryPath, "*", options).ToList();
            var filePaths = entries.Where(e => !Directory.Exists(e)).OrderBy(Path.GetFileName).ToList();
            var subDirPaths = entries.Where(e => Directory.Exists(e)).OrderBy(Path.GetFileName).ToList();

            var currentLevelNodes = new List<FileNode>();

            // Procesar archivos
            foreach (var file in filePaths)
            {
                var relativePath = GetRelativePath(_rootPath, file);
                var isExcluded = _exclusionEngine.IsExcluded(file, relativePath, false);
                
                // NUEVO: Early exit absoluto. Si está excluido, no existe para CodeBlast.
                if (isExcluded) continue;
                
                var isBinaryFast = BinaryDetector.IsBinaryByExtension(file);
                var needsVerification = !isBinaryFast && string.IsNullOrEmpty(Path.GetExtension(file));

                var node = new FileNode
                {
                    Name = Path.GetFileName(file),
                    FullPath = file,
                    RelativePath = relativePath,
                    IsDirectory = false,
                    IsExcluded = false, // Siempre false porque los excluidos no llegan aquí
                    IsBinary = isBinaryFast,
                    NeedsBinaryVerification = needsVerification,
                    SizeBytes = isBinaryFast ? 0 : new FileInfo(file).Length,
                    Parent = parentNode
                };
                currentLevelNodes.Add(node);
            }

            // Procesar directorios
            var subTasks = new List<(FileNode Node, string Path)>();
            foreach (var subdir in subDirPaths)
            {
                var dirName = Path.GetFileName(subdir);
                var relativePath = GetRelativePath(_rootPath, subdir);
                var isExcluded = _exclusionEngine.IsExcluded(subdir, relativePath, true);

                if (dirName == ".git" || isExcluded) 
                    continue; // NUEVO: No creamos el nodo ni lo añadimos a la recursión

                var dirNode = new FileNode
                {
                    Name = dirName,
                    FullPath = subdir,
                    RelativePath = relativePath,
                    IsDirectory = true,
                    IsExcluded = false,
                    Parent = parentNode
                };

                currentLevelNodes.Add(dirNode);
                subTasks.Add((dirNode, subdir));
            }

            nodes.AddRange(currentLevelNodes);

            // Recursión paralela real usando el ThreadPool
            if (subTasks.Count > 0)
            {
                await Task.WhenAll(subTasks.Select(t => Task.Run(() => ScanDirectoryAsync(t.Path, t.Node, t.Node.Children))));
            }
            Logger.Log($"Completed directory scan: {directoryPath}");
        }
        catch (UnauthorizedAccessException) { Logger.Log($"Unauthorized access: {directoryPath}", "WARN"); }
        catch (IOException ex) { Logger.Log($"IO error scanning {directoryPath}: {ex.Message}", "WARN"); }
    }

    private static string GetRelativePath(string rootPath, string fullPath)
    {
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            return fullPath;

        var relative = fullPath[rootPath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relative;
    }
}
