using System.IO;
using System.Windows;
using System.Windows.Input;

namespace CodeBlast.App.Views;

public partial class IgnoreEditorWindow : Window
{
    private readonly string _ignoreFilePath;
    public bool WasSaved { get; private set; }

    public IgnoreEditorWindow(string projectPath)
    {
        InitializeComponent();
        _ignoreFilePath = Path.Combine(projectPath, ".codeblastignore");
        LoadFile();
    }

    private void LoadFile()
    {
        if (File.Exists(_ignoreFilePath))
        {
            EditorTextBox.Text = File.ReadAllText(_ignoreFilePath);
        }
        else
        {
            EditorTextBox.Text = 
@"# Archivo de exclusiones personalizadas de CodeBlast
# Funciona igual que .gitignore. Usa '/' para carpetas y '*' para comodines.

# Ejemplos:
# /docs/
# *.tmp
# secretos.json

";
            EditorTextBox.CaretIndex = EditorTextBox.Text.Length;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    private void CloseWindow_Click(object sender, RoutedEventArgs e) => CloseDialog(false);
    private void Cancel_Click(object sender, RoutedEventArgs e) => CloseDialog(false);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        File.WriteAllText(_ignoreFilePath, EditorTextBox.Text);
        CloseDialog(true);
    }

    private void CloseDialog(bool saved)
    {
        WasSaved = saved;
        DialogResult = saved;
        Close();
    }
}
