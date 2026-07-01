using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherApp.Core.Geocoding;
using WeatherApp.Core.Time;
using WeatherApp.Core.ViewModels;
using WeatherApp.Core.Weather;

namespace WeatherApp;

/// <summary>
/// Application composition root: builds the .NET generic host (DI + logging),
/// then on startup resolves the MainWindow and its MainViewModel and shows it.
/// App.xaml carries no StartupUri — OnStartup owns window creation.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            // Two typed HttpClients with distinct HTTPS base hosts: the Geocoder
            // (place search) and the Weather Provider (forecast). Both stay https —
            // the user's searched location/coordinates travel over these hops and
            // must remain encrypted and authenticated (no TLS-validation weakening).
            services.AddHttpClient<IGeocoder, OpenMeteoGeocoder>(c =>
                c.BaseAddress = new Uri("https://geocoding-api.open-meteo.com/"));
            services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(c =>
                c.BaseAddress = new Uri("https://api.open-meteo.com/"));

            services.AddSingleton<WmoConditionMap>();
            services.AddTransient<IDebounceScheduler, DebounceScheduler>();
            services.AddTransient<SearchViewModel>();
            services.AddTransient<WeatherViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainWindow>();
        })
        .Build();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Dispose();
        base.OnExit(e);
    }
}
