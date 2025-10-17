using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MyGames.Desktop.Logs;
using MyGames.Desktop.Services;
using MyGames.Desktop.ViewModels;
using System.Windows;

namespace MyGames.Desktop;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        IConfigurationRoot? builtConfig = null;

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                builtConfig = config.Build();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<AppSettings>(builtConfig!.GetSection("App"));
                services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);

                services.AddSingleton<LoggerService>();

                // use Startup.cs instead (2 host --> 1 host)
                /**********************************************/
                // register hosted service with kestrel url built from settings
                //services.AddHostedService<HttpListenerService>();

                services.AddSingleton<GamePersistenceService>();
                services.AddSingleton<ChessGameService>();
                services.AddSingleton<StockfishService>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            // ConfigureWebHostDefaults ensures Kestrel is available and sets up Startup
            .ConfigureWebHostDefaults(webBuilder =>
            {
                var appSettings = builtConfig!.GetSection("App").Get<AppSettings>() ?? new AppSettings();

                webBuilder.UseStartup<Startup>();
                webBuilder.UseUrls($"{appSettings.SchemeDomain}:{appSettings.HttpPort}");
            })
            .Build();

        await _host.StartAsync();

        var win = _host.Services.GetRequiredService<MainWindow>();
        win.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}