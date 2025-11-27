using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using BattleShipGame2.ViewModels;
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
        _gameServer = new GameServer(8889); // Используем порт 8889
        _ = _gameServer.StartAsync(); // Запускаем в фоне
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            desktop.ShutdownRequested += OnShutdownRequested;
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://BattleShipGame2/Assets/BattleShipGame.ico")));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
    
    private void OnShutdownRequested(object sender, ShutdownRequestedEventArgs e)
    {
        _gameServer?.Stop();
    }
}