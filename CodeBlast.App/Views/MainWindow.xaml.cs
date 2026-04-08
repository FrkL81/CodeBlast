using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CodeBlast.App.ViewModels;
using CodeBlast.Core.Models;
using CodeBlast.Core.Services;
using WPFMessageBox = System.Windows.MessageBox;
using WPFMessageBoxImage = System.Windows.MessageBoxImage;
using WPFMessageBoxButton = System.Windows.MessageBoxButton;
using WinFormsDialog = System.Windows.Forms.DialogResult;
using WinFormsSaveFileDialog = System.Windows.Forms.SaveFileDialog;

namespace CodeBlast.App.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        StateChanged += (_, _) => UpdateMaxRestoreIcon();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxRestoreWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaxRestoreIcon()
    {
        if (MaxRestoreButton != null)
            MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Seleccionar carpeta del proyecto"
        };

        if (dialog.ShowDialog() == true)
        {
            if (_viewModel != null)
            {
                var button = sender as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "⏳ Cargando...";
                }

                await _viewModel.OpenFolderAsync(dialog.FolderName);
                ProjectPathText.Text = dialog.FolderName;
                RefreshBtn.IsEnabled = true;

                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = "📁 Abrir carpeta";
                }
            }
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var settingsWindow = new SettingsWindow(_viewModel.RespectGitIgnore, _viewModel.ProjectPath)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            _viewModel.RespectGitIgnore = settingsWindow.RespectGitIgnore;

            if (settingsWindow.RequiresRescan && !string.IsNullOrEmpty(_viewModel.ProjectPath))
            {
                // Recargar el proyecto automáticamente para aplicar las nuevas exclusiones
                await _viewModel.OpenFolderAsync(_viewModel.ProjectPath);
            }
        }
    }

    private void Donation_Click(object sender, RoutedEventArgs e)
    {
        var donationWindow = new DonationWindow
        {
            Owner = this
        };
        donationWindow.ShowDialog();
    }

    private async void RefreshProject_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null || string.IsNullOrEmpty(_viewModel.ProjectPath)) return;

        RefreshBtn.IsEnabled = false;
        var originalContent = RefreshBtn.Content;
        RefreshBtn.Content = "⏳...";

        await _viewModel.RefreshProjectAsync();

        RefreshBtn.Content = originalContent;
        RefreshBtn.IsEnabled = true;
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.FilterNodes(SearchBox.Text);
        }
    }

    private async void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileNode node && _viewModel != null)
        {
            Logger.Log($"FileTree_SelectedItemChanged: {node.Name}");
            await _viewModel.SetPreviewFileAsync(node);
        }
        else
        {
            Logger.Log($"FileTree_SelectedItemChanged: node is null or viewModel is null", "DEBUG");
        }
    }

    private async void FileNameText_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.DataContext is FileNode node && _viewModel != null)
        {
            Logger.Log($"FileNameText_MouseLeftButtonDown: {node.Name}");
            await _viewModel.SetPreviewFileAsync(node);
            e.Handled = true; // Evitar que el TreeView procese el click
        }
    }

    private void PreviewTab_Click(object sender, RoutedEventArgs e)
    {
        PreviewPanel.Visibility = Visibility.Visible;
        OutputPanel.Visibility = Visibility.Collapsed;
        SetTabActive(PreviewTabBtn, true);
        SetTabActive(OutputTabBtn, false);
    }

    private void OutputTab_Click(object sender, RoutedEventArgs e)
    {
        PreviewPanel.Visibility = Visibility.Collapsed;
        OutputPanel.Visibility = Visibility.Visible;
        SetTabActive(PreviewTabBtn, false);
        SetTabActive(OutputTabBtn, true);
    }

    private static void SetTabActive(System.Windows.Controls.Button btn, bool active)
    {
        // Walk the template to update border and text color
        if (btn.Template.FindName("TabBorder", btn) is Border border)
            border.BorderBrush = active
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6E6EFF"))
                : System.Windows.Media.Brushes.Transparent;

        if (btn.Template.FindName("TabText", btn) is TextBlock text)
            text.Foreground = active
                ? new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E8E8F0"))
                : new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9090A8"));
    }
    private async void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
            await _viewModel.SelectAllAsync();
    }

    private async void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
            await _viewModel.DeselectAllAsync();
    }

    private async void GenerateOutput_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        var button = sender as System.Windows.Controls.Button;
        if (button != null) button.IsEnabled = false;

        var mode = GetOutputMode();
        var payload = await _viewModel.GeneratePayloadAsync(mode);

        _viewModel.OutputText = payload;

        // Cambiar al tab Output automáticamente
        OutputTab_Click(this, new RoutedEventArgs());

        if (button != null)
        {
            button.IsEnabled = true;
            var originalContent = button.Content;
            button.Content = "✓ Generado";
            await Task.Delay(1500);
            button.Content = originalContent;
        }
    }

    private async void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        // Si no hay output generado, generar primero
        if (string.IsNullOrEmpty(_viewModel.OutputText) || _viewModel.OutputText == "El output generado aparecerá aquí")
        {
            await GenerateOutputInternal();
        }

        System.Windows.Clipboard.SetText(_viewModel.OutputText);

        var button = sender as System.Windows.Controls.Button;
        if (button != null)
        {
            var originalContent = button.Content;
            button.Content = "✓ Copiado";
            await Task.Delay(1500);
            button.Content = originalContent;
        }
    }

    private async Task GenerateOutputInternal()
    {
        if (_viewModel == null)
            return;

        var mode = GetOutputMode();
        var payload = await _viewModel.GeneratePayloadAsync(mode);
        _viewModel.OutputText = payload;
    }

    private async void ExportFile_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var settingsService = new SettingsService();
        var settings = settingsService.LoadSettings();
        bool isMarkdown = settings.OutputFormat == "Markdown";
        
        var ext = isMarkdown ? "md" : "txt";
        var filter = isMarkdown ? "Archivos Markdown|*.md" : "Archivos de texto|*.txt";

        var saveDialog = new WinFormsSaveFileDialog
        {
            Filter = filter,
            Title = $"Exportar como {ext.ToUpper()}"
        };
        
        var projectName = string.IsNullOrEmpty(_viewModel.ProjectPath) 
            ? "output" 
            : System.IO.Path.GetFileName(_viewModel.ProjectPath);
            
        saveDialog.FileName = $"{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";

        if (saveDialog.ShowDialog() == WinFormsDialog.OK)
        {
            if (string.IsNullOrEmpty(_viewModel.OutputText) || _viewModel.OutputText.Contains("aparecerá aquí"))
            {
                await GenerateOutputInternal();
            }

            var exportService = new CodeBlast.Core.Services.ExportService();
            await exportService.ExportToFileAsync(_viewModel.OutputText, saveDialog.FileName);

            WPFMessageBox.Show($"Archivo exportado correctamente como .{ext}", "CodeBlast",
                WPFMessageBoxButton.OK, WPFMessageBoxImage.Information);
        }
    }

    private OutputMode GetOutputMode()
    {
        if (FileMapOnlyRadio.IsChecked == true)
            return OutputMode.FileMapOnly;
        if (ContentOnlyRadio.IsChecked == true)
            return OutputMode.ContentOnly;
        return OutputMode.Both;
    }
}
