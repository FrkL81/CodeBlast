using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBlast.Core.Collections;
using CodeBlast.Core.Models;
using CodeBlast.Core.Services;
using System.Threading;
using CoreCheckState = CodeBlast.Core.Models.CheckState;

namespace CodeBlast.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsService _settingsService;
    private readonly GitIgnoreParser _gitIgnoreParser;
    private readonly CustomRulesService _customRulesService;
    private readonly ExclusionEngine _exclusionEngine;

    private bool _respectGitIgnore = true;
    private bool _isLoading;
    private string _projectPath = string.Empty;
    private int _selectedFileCount;
    private int _rawTokenCount;
    private int _outputTokenCount;
    private string _previewText = "Seleccione un archivo para previsualizar";
    private string _outputText = "El output generado aparecerá aquí";
    private string _previewFileName = string.Empty;
    private string _previewMeta = string.Empty;
    private System.Windows.Visibility _previewHeaderVisibility = System.Windows.Visibility.Collapsed;
    private double _splitterPosition = 0.35;
    
    private BulkObservableCollection<FileNode> _rootNodes = new();
    private readonly HashSet<FileNode> _selectedNodes = new();
    private readonly object _selectedNodesLock = new(); 
    private readonly TokenCache _tokenCache = new();
    private CancellationTokenSource _tokenCts = new();

    public BulkObservableCollection<FileNode> RootNodes
    {
        get => _rootNodes;
        set { _rootNodes = value; OnPropertyChanged(nameof(RootNodes)); }
    }

    public string ProjectPath
    {
        get => _projectPath;
        set { _projectPath = value; OnPropertyChanged(nameof(ProjectPath)); }
    }

    public int SelectedFileCount
    {
        get => _selectedFileCount;
        set { _selectedFileCount = value; OnPropertyChanged(nameof(SelectedFileCount)); }
    }

    public int RawTokenCount
    {
        get => _rawTokenCount;
        set { _rawTokenCount = value; OnPropertyChanged(nameof(RawTokenCount)); }
    }

    public int OutputTokenCount
    {
        get => _outputTokenCount;
        set { _outputTokenCount = value; OnPropertyChanged(nameof(OutputTokenCount)); }
    }

    public string PreviewText
    {
        get => _previewText;
        set { _previewText = value; OnPropertyChanged(nameof(PreviewText)); }
    }

    public string OutputText
    {
        get => _outputText;
        set { _outputText = value; OnPropertyChanged(nameof(OutputText)); }
    }

    public string PreviewFileName
    {
        get => _previewFileName;
        set { _previewFileName = value; OnPropertyChanged(nameof(PreviewFileName)); }
    }

    public string PreviewMeta
    {
        get => _previewMeta;
        set { _previewMeta = value; OnPropertyChanged(nameof(PreviewMeta)); }
    }

    public System.Windows.Visibility PreviewHeaderVisibility
    {
        get => _previewHeaderVisibility;
        set { _previewHeaderVisibility = value; OnPropertyChanged(nameof(PreviewHeaderVisibility)); }
    }

    public double SplitterPosition
    {
        get => _splitterPosition;
        set { _splitterPosition = value; OnPropertyChanged(nameof(SplitterPosition)); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
    }

    private const int PreviewMaxChars = 20_000;
    private const int PreviewMaxLines = 300;

    public MainViewModel()
    {
        _settingsService = new SettingsService();
        _gitIgnoreParser = new GitIgnoreParser();
        _customRulesService = new CustomRulesService();
        _exclusionEngine = new ExclusionEngine(_gitIgnoreParser, _customRulesService);
        _exclusionEngine.SetRespectGitIgnore(_respectGitIgnore);
    }

    public bool RespectGitIgnore
    {
        get => _respectGitIgnore;
        set
        {
            _respectGitIgnore = value;
            _exclusionEngine.SetRespectGitIgnore(value);
            OnPropertyChanged(nameof(RespectGitIgnore));
        }
    }

    public async Task OpenFolderAsync(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return;

        try
        {
            Logger.Log($"Opening folder: {folderPath}");
            IsLoading = true;
            // NUEVO: Limpiar paneles
            OutputText = "El output generado aparecerá aquí";
            PreviewText = "Seleccione un archivo para previsualizar";
            PreviewFileName = string.Empty;
            PreviewMeta = string.Empty;
            PreviewHeaderVisibility = System.Windows.Visibility.Collapsed;

            // Forzar actualización del UI antes de empezar el trabajo pesado
            await Task.Delay(50);

            ProjectPath = folderPath;
            RootNodes.Clear();
            lock (_selectedNodesLock) { _selectedNodes.Clear(); } 
            _tokenCache.Initialize(folderPath);
            await _tokenCache.LoadAsync();
            Logger.Log("Token cache loaded.");

            await _gitIgnoreParser.LoadFromDirectoryAsync(folderPath);
            Logger.Log("Gitignore rules loaded.");

            // NUEVO: Cargar reglas globales + reglas personalizadas del proyecto
            var settings = _settingsService.LoadSettings();
            var currentRules = settings.GlobalExclusions.ToList();
            
            // NUEVO: Ocultar el archivo caché si el usuario lo solicita en la configuración
            if (settings.ExcludeCacheFile)
            {
                currentRules.Add(".codeblast-cache.json");
            }

            var customRulesPath = _settingsService.GetCustomRulesPath(folderPath);
            if (File.Exists(customRulesPath))
            {
                var projectRules = File.ReadAllLines(customRulesPath).Where(r => !string.IsNullOrWhiteSpace(r));
                currentRules.AddRange(projectRules);
            }
            
            _customRulesService.SetRules(currentRules.Distinct());
            Logger.Log("Global and Custom rules loaded.");

            var scanner = new FileSystemScanner(folderPath, _exclusionEngine);
            var nodes = await scanner.ScanAsync();
            Logger.Log($"Scan complete. Found {nodes.Count} branch structures.");

            foreach (var node in nodes)
            {
                SubscribeToAllNodes(node); 
            }

            RootNodes.AddRange(nodes);
            Logger.Log("Nodes added to RootNodes.");

            // Seleccionar todos los archivos automáticamente (en background)
            // Seleccionar todos los archivos automáticamente (en background)
            await Task.Run(() => SetAllNodesCheckState(RootNodes, CoreCheckState.Checked));
            Logger.Log($"All nodes check state set to Checked. Selected index size: {_selectedNodes.Count}");
            
            // Calcular tokens
            ScheduleTokenRecalculation();
            Logger.Log("Token recalculation scheduled.");

            // Verificar binarios desconocidos en background
            _ = Task.Run(() => VerifyUnknownBinaryFilesAsync(nodes));
            Logger.Log("Background binary verification started.");

            // Mostrar el primer archivo en el preview
            var firstFile = FindFirstFile(RootNodes);
            if (firstFile != null)
                await SetPreviewFileAsync(firstFile);

            IsLoading = false;
            Logger.Log("Folder opened successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in OpenFolderAsync", ex);
            IsLoading = false;
            System.Windows.MessageBox.Show($"Ocurrió un error al abrir la carpeta: {ex.Message}", "Error fatal", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void SubscribeToAllNodes(FileNode node)
    {
        node.PropertyChanged += OnNodePropertyChanged;
        if (node.IsDirectory)
        {
            foreach (var child in node.Children)
            {
                SubscribeToAllNodes(child);
            }
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is FileNode node && e.PropertyName == nameof(FileNode.CheckState))
        {
            // Administramos el HashSet inmediatamente solo con el nodo afectado
            if (!node.IsDirectory)
            {
                lock (_selectedNodesLock)
                {
                    if (node.CheckState == CoreCheckState.Checked && !node.IsExcluded && !node.IsBinary)
                        _selectedNodes.Add(node);
                    else
                        _selectedNodes.Remove(node);
                }
            }
            
            ScheduleTokenRecalculation();
        }
    }

    private async Task VerifyUnknownBinaryFilesAsync(IEnumerable<FileNode> nodes)
    {
        try
        {
            Logger.Log("Starting unknown binary verification.");
            await VerifyNodesRecursiveAsync(nodes);
            Logger.Log("Finished unknown binary verification.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in VerifyUnknownBinaryFilesAsync", ex);
        }
    }

    private async Task VerifyNodesRecursiveAsync(IEnumerable<FileNode> nodes)
    {
        bool changed = false;
        foreach (var node in nodes)
        {
            if (node.IsDirectory)
            {
                await VerifyNodesRecursiveAsync(node.Children);
                continue;
            }

            if (node.NeedsBinaryVerification)
            {
                var isBinary = BinaryDetector.IsBinary(node.FullPath);
                if (isBinary)
                {
                    node.IsBinary = true;
                    // Si estaba seleccionado, hay que quitarlo del index y recalcular tokens
                    lock (_selectedNodesLock)
                    {
                        if (_selectedNodes.Contains(node))
                        {
                            _selectedNodes.Remove(node);
                            changed = true;
                        }
                    }
                }
                node.NeedsBinaryVerification = false;
            }
        }

        if (changed)
        {
            ScheduleTokenRecalculation();
        }
    }

    private void ScheduleTokenRecalculation()
    {
        _tokenCts.Cancel();
        _tokenCts = new CancellationTokenSource();
        var token = _tokenCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(200, token);
                if (!token.IsCancellationRequested)
                    await CalculateTokensAsync(token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.LogError("Error in ScheduleTokenRecalculation", ex);
            }
        });
    }

    private FileNode? FindFirstFile(ObservableCollection<FileNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (!node.IsDirectory && !node.IsExcluded && !node.IsBinary)
                return node;
            if (node.IsDirectory)
            {
                var found = FindFirstFile(node.Children);
                if (found != null)
                    return found;
            }
        }
        return null;
    }


    public async Task SelectAllAsync()
    {
        IsLoading = true;
        await Task.Run(() => SetAllNodesCheckState(RootNodes, CoreCheckState.Checked));
        Logger.Log($"SelectAllAsync complete. Selected: {_selectedNodes.Count} files.");
        ScheduleTokenRecalculation();
        IsLoading = false;
    }

    public async Task DeselectAllAsync()
    {
        IsLoading = true;
        await Task.Run(() => SetAllNodesCheckState(RootNodes, CoreCheckState.Unchecked));
        Logger.Log($"DeselectAllAsync complete. Selected: {_selectedNodes.Count} files.");
        ScheduleTokenRecalculation();
        IsLoading = false;
    }

    public void SelectAll()
    {
        SetAllNodesCheckState(RootNodes, CoreCheckState.Checked);
        ScheduleTokenRecalculation();
    }

    public void DeselectAll()
    {
        SetAllNodesCheckState(RootNodes, CoreCheckState.Unchecked);
        ScheduleTokenRecalculation();
    }

    private CancellationTokenSource? _searchCts;

    public async void FilterNodes(string searchText)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            // Debounce de 250ms para no saturar la UI si el usuario teclea rápido
            await Task.Delay(250, token);

            if (string.IsNullOrWhiteSpace(searchText))
            {
                ResetVisibility(RootNodes);
                return;
            }

            var query = searchText.Trim().ToLowerInvariant();
            foreach (var node in RootNodes)
            {
                FilterNodesRecursive(node, query);
            }
        }
        catch (TaskCanceledException) { /* Ignorar si se canceló por una nueva tecla */ }
        catch (OperationCanceledException) { }
    }

    private bool FilterNodesRecursive(FileNode node, string query)
    {
        if (!node.IsDirectory)
        {
            node.IsVisible = node.Name.ToLowerInvariant().Contains(query);
            return node.IsVisible;
        }

        bool anyChildVisible = false;
        foreach (var child in node.Children)
        {
            var childVisible = FilterNodesRecursive(child, query);
            if (childVisible) anyChildVisible = true;
        }

        // Una carpeta es visible si su propio nombre coincide O si algún hijo coincide
        bool nameMatches = node.Name.ToLowerInvariant().Contains(query);
        node.IsVisible = nameMatches || anyChildVisible;
        
        // Auto-expandir la carpeta para facilitar la vista, pero solo si la coincidencia está adentro
        node.IsExpanded = anyChildVisible;

        return node.IsVisible;
    }

    private void ResetVisibility(IEnumerable<FileNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.IsVisible = true;
            if (node.IsDirectory)
            {
                node.IsExpanded = false; // Contraer todo al limpiar la búsqueda
                ResetVisibility(node.Children);
            }
        }
    }

    public string GeneratePayload(OutputMode mode)
    {
        return GeneratePayloadAsync(mode).GetAwaiter().GetResult();
    }

    public string GenerateMarkdown()
    {
        return GenerateMarkdownAsync().GetAwaiter().GetResult();
    }

    public async Task<string> GeneratePayloadAsync(OutputMode mode)
    {
        List<FileNode> selectedNodes;
        lock (_selectedNodesLock) 
        { 
            selectedNodes = _selectedNodes.ToList(); 
        }
        var projectName = string.IsNullOrEmpty(ProjectPath) ? "Unknown" : Path.GetFileName(ProjectPath);
        var builder = new PayloadBuilder(mode, projectName);
        
        var settings = _settingsService.LoadSettings();
        if (settings.OutputFormat.Equals("Markdown", StringComparison.OrdinalIgnoreCase))
        {
            // Usa el generador de Markdown si está configurado así
            return await builder.BuildMarkdownAsync(selectedNodes);
        }
        
        // Por defecto, usa XML-Like
        return await builder.BuildAsync(selectedNodes);
    }

    public async Task<string> GenerateMarkdownAsync()
    {
        List<FileNode> selectedNodes;
        lock (_selectedNodesLock) 
        { 
            selectedNodes = _selectedNodes.ToList(); 
        }
        var projectName = string.IsNullOrEmpty(ProjectPath) ? "Unknown" : Path.GetFileName(ProjectPath);
        var builder = new PayloadBuilder(OutputMode.Both, projectName);
        return await builder.BuildMarkdownAsync(selectedNodes);
    }

    public async Task SetPreviewFileAsync(FileNode node)
    {
        if (node == null)
        {
            Logger.Log("SetPreviewFileAsync: node is null", "WARN");
            return;
        }

        Logger.Log($"SetPreviewFileAsync called: {node.Name}, IsDirectory: {node.IsDirectory}");

        if (node.IsDirectory || !File.Exists(node.FullPath))
        {
            Logger.Log($"SetPreviewFileAsync: Skipping - IsDirectory={node.IsDirectory}, Exists={File.Exists(node.FullPath)}", "DEBUG");
            return;
        }

        PreviewFileName = node.Name;

        var sizeKb = node.SizeBytes > 0 ? node.SizeBytes / 1024.0 : new FileInfo(node.FullPath).Length / 1024.0;
        var tokens = node.TokenCount.HasValue ? $"~{node.TokenCount:N0} tokens" : "tokens: —";
        PreviewMeta = $"{node.RelativePath}  ·  {sizeKb:F1} KB  ·  {tokens}";
        PreviewHeaderVisibility = System.Windows.Visibility.Visible;

        try
        {
            var content = await FileContentReader.ReadTextAsync(node.FullPath);
            var lines = content.Split('\n');
            bool truncated = false;

            if (lines.Length > PreviewMaxLines)
            {
                content = string.Join('\n', lines.Take(PreviewMaxLines));
                truncated = true;
            }
            else if (content.Length > PreviewMaxChars)
            {
                content = content[..PreviewMaxChars];
                truncated = true;
            }

            PreviewText = truncated ? content + $"\n\n[... truncado — mostrando primeras {PreviewMaxLines} líneas]" : content;
            Logger.Log($"SetPreviewFileAsync: PreviewText set, length={PreviewText.Length}", "DEBUG");
        }
        catch (Exception ex)
        {
            PreviewText = "No se pudo leer el archivo.";
            Logger.LogError($"SetPreviewFileAsync: Error reading file", ex);
        }
    }

    public async Task CalculateTokensAsync(CancellationToken ct = default)
    {
        List<FileNode> selectedNodes;
        lock (_selectedNodesLock) 
        { 
            selectedNodes = _selectedNodes.ToList(); 
        }
        int totalRawTokens = 0;
        int totalWrapperTokens = 0;
        Logger.Log($"Starting token calculation for {selectedNodes.Count} files...");

        // Cargar configuración para saber qué formato usamos (XmlLike o Markdown)
        var settings = _settingsService.LoadSettings();
        bool isMarkdown = settings.OutputFormat.Equals("Markdown", StringComparison.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(
            selectedNodes,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2, CancellationToken = ct },
            async (node, innerCt) =>
            {
                try
                {
                    // 1. Tokens del contenido crudo
                    var cached = _tokenCache.TryGet(node.FullPath);
                    if (cached.HasValue)
                    {
                        node.TokenCount = cached.Value;
                    }
                    else
                    {
                        var content = await FileContentReader.ReadTextAsync(node.FullPath, innerCt);
                        var count = TokenCounter.CountTokens(content);
                        node.TokenCount = count;
                        _tokenCache.Set(node.FullPath, count);
                    }
                    Interlocked.Add(ref totalRawTokens, node.TokenCount ?? 0);

                    // 2. Tokens de la sobrecarga (Wrapper) de este archivo según el formato
                    string wrapperOverhead;
                    if (isMarkdown)
                    {
                        var ext = Path.GetExtension(node.Name).TrimStart('.');
                        // Asumimos un "language hint" genérico de 5 letras promedio si no queremos calcularlo exacto
                        wrapperOverhead = $"\n### {node.RelativePath}\n\n```{ext}\n\n```\n";
                    }
                    else
                    {
                        wrapperOverhead = $"<file path=\"{node.RelativePath}\">\n\n</file>\n\n";
                    }
                    
                    var wrapperTokens = TokenCounter.CountTokens(wrapperOverhead);
                    Interlocked.Add(ref totalWrapperTokens, wrapperTokens);
                }
                catch (OperationCanceledException) { throw; }
                catch { node.TokenCount = 0; }
            });

        if (ct.IsCancellationRequested) return;

        // 3. Tokens del File Map (Árbol de directorios)
        var projectName = string.IsNullOrEmpty(ProjectPath) ? "Unknown" : Path.GetFileName(ProjectPath);
        var dummyBuilder = new PayloadBuilder(OutputMode.Both, projectName);
        var fileMapString = dummyBuilder.BuildFileMap(selectedNodes);
        
        // Si es Markdown, el PayloadBuilder añade "## File Map\n```\n...\n```\n## Files\n"
        if (isMarkdown)
        {
            fileMapString = $"## File Map\n\n```\n{fileMapString}```\n\n## Files\n";
        }
        
        int fileMapTokens = TokenCounter.CountTokens(fileMapString);

        // Actualizar UI
        RawTokenCount = totalRawTokens;
        OutputTokenCount = totalRawTokens + totalWrapperTokens + fileMapTokens;
        SelectedFileCount = selectedNodes.Count;
        
        Logger.Log($"Token calculation finished: Raw: {RawTokenCount}, Output: {OutputTokenCount}", "DEBUG");
        await _tokenCache.SaveAsync();
    }


    public record LoadProgress(string Message, int Percent);

    private void SetAllNodesCheckState(ObservableCollection<FileNode> nodes, CoreCheckState state)
    {
        // Solo necesitamos setear los nodos raíz; la propagación interna de FileNode se encarga del resto
        foreach (var node in nodes)
        {
            node.CheckState = state;
        }
    }

    public async Task RefreshProjectAsync()
    {
        if (string.IsNullOrEmpty(ProjectPath) || !Directory.Exists(ProjectPath)) return;

        try
        {
            Logger.Log("Refreshing project...");
            IsLoading = true;

            // 1. Guardar la selección actual
            var selectedPaths = new HashSet<string>();
            lock (_selectedNodesLock)
            {
                foreach (var node in _selectedNodes)
                {
                    selectedPaths.Add(node.FullPath);
                }
            }

            // 2. Limpiar UI temporalmente
            PreviewText = "Proyecto actualizado. Seleccione un archivo para previsualizar.";
            PreviewFileName = string.Empty;
            PreviewMeta = string.Empty;
            PreviewHeaderVisibility = System.Windows.Visibility.Collapsed;

            RootNodes.Clear();
            lock (_selectedNodesLock) { _selectedNodes.Clear(); }

            // 3. Recargar reglas y escanear
            await _tokenCache.LoadAsync();
            await _gitIgnoreParser.LoadFromDirectoryAsync(ProjectPath);

            var settings = _settingsService.LoadSettings();
            var currentRules = settings.GlobalExclusions.ToList();
            if (settings.ExcludeCacheFile) currentRules.Add(".codeblast-cache.json");
            
            var customRulesPath = _settingsService.GetCustomRulesPath(ProjectPath);
            if (File.Exists(customRulesPath))
            {
                currentRules.AddRange(File.ReadAllLines(customRulesPath).Where(r => !string.IsNullOrWhiteSpace(r)));
            }
            _customRulesService.SetRules(currentRules.Distinct());

            var scanner = new FileSystemScanner(ProjectPath, _exclusionEngine);
            var nodes = await scanner.ScanAsync();

            foreach (var node in nodes)
            {
                SubscribeToAllNodes(node); 
            }

            RootNodes.AddRange(nodes);

            // 4. Restaurar selección anterior
            await Task.Run(() => RestoreSelection(RootNodes, selectedPaths));

            // 5. Relanzar verificaciones y cálculos
            ScheduleTokenRecalculation();
            _ = Task.Run(() => VerifyUnknownBinaryFilesAsync(nodes));

            IsLoading = false;
            Logger.Log("Project refreshed successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in RefreshProjectAsync", ex);
            IsLoading = false;
        }
    }

    private void RestoreSelection(IEnumerable<FileNode> nodes, HashSet<string> selectedPaths)
    {
        foreach (var node in nodes)
        {
            if (node.IsDirectory)
            {
                RestoreSelection(node.Children, selectedPaths);
            }
            else
            {
                // Si el archivo existía y estaba marcado, lo marcamos. 
                // Esto desencadenará automáticamente el bubbling hacia los padres.
                if (selectedPaths.Contains(node.FullPath))
                {
                    node.CheckState = CoreCheckState.Checked;
                }
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
