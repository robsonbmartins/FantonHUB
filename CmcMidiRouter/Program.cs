using Serilog;
using System;
using System.Windows.Forms;

namespace CmcMidiRouter;

static class Program
{
    [STAThread]
    static void Main()
    {
        string logDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CmcMidiRouter", "logs");
        string logFile = System.IO.Path.Combine(logDir, "cmc-router.txt");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logFile, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            Log.Information("Iniciando a aplicação CmcMidiRouter...");
            DatabaseManager.InitializeDatabase();
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Ocorreu um erro fatal durante a execução.");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }    
}