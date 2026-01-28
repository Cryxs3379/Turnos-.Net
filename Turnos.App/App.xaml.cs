using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Turnos.Data;

namespace Turnos.App;

public partial class App : Application
{
    public static TurnosRepository TurnosRepository { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configurar lectura de appsettings.json
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

        var configuration = builder.Build();

        // Leer connection string
        var connectionString = configuration.GetConnectionString("Db")
            ?? throw new InvalidOperationException("ConnectionStrings:Db no encontrada en appsettings.json");

        // Crear instancia de TurnosRepository
        TurnosRepository = new TurnosRepository(connectionString);
    }
}
