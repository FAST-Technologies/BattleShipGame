/*// App.axaml.cs - ИСПРАВЛЕННАЯ версия
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using BattleShipGame2.Services;
using BattleShipGame2.ViewModels;

namespace BattleShipGame2;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        Console.WriteLine("App.Initialize() called");
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("App.OnFrameworkInitializationCompleted() called");

        try
        {
            // Настройка Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
            Console.WriteLine("ServiceProvider built successfully");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.WriteLine("Desktop lifetime detected");

                DisableAvaloniaDataAnnotationValidation();

                // Создаем главное окно
                Console.WriteLine("Creating MainWindow...");
                var mainWindow = new Views.MainWindow();
                Console.WriteLine($"MainWindow created: {mainWindow}");

                // Устанавливаем DataContext
                var mainWindowViewModel = _serviceProvider.GetRequiredService<ViewModels.MainWindowViewModel>();
                mainWindow.DataContext = mainWindowViewModel;
                Console.WriteLine($"DataContext set: {mainWindow.DataContext}");

                desktop.MainWindow = mainWindow;
                Console.WriteLine("MainWindow assigned to desktop");

                // Подписываемся на события для отладки
                mainWindow.Opened += (s, e) => Console.WriteLine("MainWindow opened successfully!");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("Base.OnFrameworkInitializationCompleted() called");
    }

    private void ConfigureServices(IServiceCollection services)
    {
        Console.WriteLine("Configuring services...");

        // Регистрируем сервисы
        services.AddSingleton<IGameService, GameService>();
        Console.WriteLine("GameService registered");

        // NavigationService должен быть Singleton
        services.AddSingleton<INavigationService, NavigationService>();
        Console.WriteLine("NavigationService registered");

        // ViewModels (Transient или Scoped)
        services.AddTransient<MenuViewModel>();
        services.AddTransient<DifficultySelectionViewModel>();
        services.AddTransient<NetworkConnectionViewModel>();
        services.AddTransient<ShipPlacementViewModel>();
        services.AddTransient<GameViewModel>();

        // MainWindowViewModel должен быть Singleton
        services.AddSingleton<MainWindowViewModel>();

        Console.WriteLine("All services configured");
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}*/
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using BattleShipGame2.Views;
using BattleShipGame2.ServerLogic;
using Avalonia.Controls;
using Avalonia.Platform;
using System;

namespace BattleShipGame2;

public partial class App : Application
{
    private GameServer? _gameServer;
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _gameServer = new GameServer(8889); // Используем порт 8889 по умолчанию
        _ = _gameServer.StartAsync(); // Запускаем в фоне
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnShutdownRequested;
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow();
            desktop.MainWindow.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://BattleShipGame2/Assets/BattleShipGame.ico")));
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
    
    private void OnShutdownRequested(object sender, ShutdownRequestedEventArgs e)
    {
        _gameServer?.Stop();
    }
}