using System.Windows;
using CodeBlast.Core.Services;

namespace CodeBlast.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private AppSettings _settings;
    private readonly string _projectPath;

    public bool RespectGitIgnore { get; private set; }
    public bool RequiresRescan { get; private set; }

    public SettingsWindow(bool currentRespectGitIgnore, string projectPath)
    {
        InitializeComponent();
        _settingsService = new SettingsService();
        _settings = _settingsService.LoadSettings();
        _projectPath = projectPath;
        
        RespectGitIgnore = currentRespectGitIgnore;
        RespectGitIgnoreCheckBox.IsChecked = currentRespectGitIgnore;
        ExcludeCacheCheckBox.IsChecked = _settings.ExcludeCacheFile;

        // Cargar formato actual
        FormatMdRadio.IsChecked = _settings.OutputFormat == "Markdown";
        FormatXmlRadio.IsChecked = _settings.OutputFormat != "Markdown";

        // Deshabilitar el botón si no hay un proyecto abierto
        if (string.IsNullOrEmpty(_projectPath))
        {
            EditIgnoreBtn.IsEnabled = false;
            EditIgnoreBtn.Content = "Abre un proyecto primero";
        }
    }

    private void EditIgnore_Click(object sender, RoutedEventArgs e)
    {
        var editor = new IgnoreEditorWindow(_projectPath) { Owner = this };
        if (editor.ShowDialog() == true && editor.WasSaved)
        {
            RequiresRescan = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var newRespectGitIgnore = RespectGitIgnoreCheckBox.IsChecked == true;
        if (RespectGitIgnore != newRespectGitIgnore)
        {
            RespectGitIgnore = newRespectGitIgnore;
            RequiresRescan = true;
        }

        _settings.ExcludeCacheFile = ExcludeCacheCheckBox.IsChecked == true;
        
        // Guardar nuevo formato y forzar recálculo si cambió
        var newFormat = FormatMdRadio.IsChecked == true ? "Markdown" : "XmlLike";
        if (_settings.OutputFormat != newFormat)
        {
            _settings.OutputFormat = newFormat;
            RequiresRescan = true; // Esto recargará el proyecto y recalculará los tokens exactos
        }

        _settingsService.SaveSettings(_settings);
        
        DialogResult = true;
        Close();
    }
}
