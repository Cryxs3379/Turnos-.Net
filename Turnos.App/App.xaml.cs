using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Turnos.Data;

namespace Turnos.App;

public partial class App : Application
{
    public static TurnosRepository TurnosRepository { get; private set; } = null!;
    public static IConfiguration Configuration { get; private set; } = null!;

    public App()
    {
        // Registrar handlers globales de excepciones ANTES de OnStartup
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Configurar lectura de appsettings.json usando AppContext.BaseDirectory
            var basePath = AppContext.BaseDirectory;
            var appsettingsPath = Path.Combine(basePath, "appsettings.json");

            if (!File.Exists(appsettingsPath))
            {
                var errorMsg = $"No se encontró el archivo appsettings.json.\n\nRuta buscada: {appsettingsPath}\n\nDirectorio base: {basePath}";
                MessageBox.Show(errorMsg, "Error de Configuración", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();
            Configuration = configuration;

            // Leer connection string
            var connectionString = configuration.GetConnectionString("Db");
            if (string.IsNullOrEmpty(connectionString))
            {
                var errorMsg = $"ConnectionStrings:Db no encontrada en appsettings.json.\n\nRuta del archivo: {appsettingsPath}";
                MessageBox.Show(errorMsg, "Error de Configuración", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // Crear instancia de TurnosRepository
            TurnosRepository = new TurnosRepository(connectionString);

            // Crear y mostrar MainWindow manualmente
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            ShowException("Error al iniciar la aplicación", ex);
            Shutdown();
        }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowException("Excepción no controlada (AppDomain)", ex);
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowException("Excepción no controlada (Dispatcher)", e.Exception);
        e.Handled = true; // Marcar como manejada para evitar crash
    }

    private static void ShowException(string title, Exception ex)
    {
        var message = $"{ex.Message}\n\n" +
                     $"Tipo: {ex.GetType().Name}\n\n" +
                     $"Stack Trace:\n{GetShortStackTrace(ex)}";

        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string GetShortStackTrace(Exception ex)
    {
        var stackTrace = ex.StackTrace ?? "No disponible";
        var lines = stackTrace.Split('\n');
        // Mostrar solo las primeras 10 líneas del stack trace
        var shortTrace = string.Join("\n", lines.Take(Math.Min(10, lines.Length)));
        if (lines.Length > 10)
        {
            shortTrace += $"\n... ({lines.Length - 10} líneas más)";
        }
        return shortTrace;
    }
}
