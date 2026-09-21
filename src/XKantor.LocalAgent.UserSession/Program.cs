using Serilog;
using XKantor.LocalAgent.Core.Configuration;
using XKantor.LocalAgent.UserSession;

// Trwałe logowanie (rotacja dzienna, jak Service/Program.cs) - do tej pory UserSession nie
// logował nigdzie poza tekstem ikony w zasobniku (ulotny), co utrudniało diagnozowanie
// watchdoga/odłączeń monitora tablicy po fakcie - patrz Board/KioskSupervisor.cs.
ConfigPaths.UpewnijSieZeFolderyIstnieja();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(Path.Combine(ConfigPaths.LogsDir, "usersession-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("XKantor Local Agent - UserSession start.");

    Application.SetHighDpiMode(HighDpiMode.SystemAware);
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new TrayApplicationContext());
}
catch (Exception ex)
{
    Log.Fatal(ex, "XKantor Local Agent - UserSession zakończył się błędem krytycznym.");
}
finally
{
    Log.CloseAndFlush();
}
