using System.Windows;
using CodeBlast.App.ViewModels;

namespace CodeBlast.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CodeBlast.Core.Services.Logger.Log("Application Started");

        Current.DispatcherUnhandledException += (s, args) =>
        {
            CodeBlast.Core.Services.Logger.LogError("UNHANDLED UI EXCEPTION", args.Exception);
            System.Windows.MessageBox.Show($"Ocurrió un error inesperado: {args.Exception.Message}", "Error fatal", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            CodeBlast.Core.Services.Logger.LogError("UNHANDLED DOMAIN EXCEPTION", args.ExceptionObject as Exception);
        };
    }
}
