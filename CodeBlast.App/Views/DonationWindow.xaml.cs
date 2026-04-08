using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace CodeBlast.App.Views;

public partial class DonationWindow : Window
{
    public DonationWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DonateLink_Click(object sender, RoutedEventArgs e)
    {
        // Placeholder para el enlace de donación (GitHub Sponsors o PayPal.me)
        var url = "https://frkl81.gumroad.com/l/CodeBlast";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true // Requerido en .NET Core / .NET 5+ para abrir URLs en el navegador web
            };
            Process.Start(psi);
        }
        catch (System.Exception ex)
        {
            CodeBlast.Core.Services.Logger.LogError("Error abriendo enlace de donación", ex);
        }
        
        Close();
    }
}
