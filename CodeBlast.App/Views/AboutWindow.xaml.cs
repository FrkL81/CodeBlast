using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace CodeBlast.App.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        
        // Cargar versión dinámica del ensamblado
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version != null)
        {
            VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenRepo_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://github.com/FrkL81/CodeBlast";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (System.Exception ex)
        {
            CodeBlast.Core.Services.Logger.LogError("Error abriendo enlace del repositorio", ex);
        }
    }
}
