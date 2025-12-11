using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using BattleShipGame.Views;
using BattleShipGame.ServerLogic;
using Avalonia.Controls;
using Avalonia.Platform;
using BattleShipGame.ViewModels;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace BattleShipGame;

/// <summary>
/// Основной класс приложения Avalonia.
/// Отвечает за инициализацию приложения, создание главного окна и управление жизненным циклом.
/// </summary>
public class App : Application
{
    private GameServer? _gameServer; /// <summary>Инициализация игрового сервера.</summary>
    
    /// <summary>
    /// Инициализирует приложение, загружая XAML-ресурсы.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Вызывается после завершения инициализации фреймворка Avalonia.
    /// Создает и запускает игровой сервер, настраивает главное окно.
    /// </summary>
    public override async void OnFrameworkInitializationCompleted()
    {
        // Запуск игрового сервера на порту 8889
        _gameServer = new GameServer(8889);
        _ = _gameServer.StartAsync(); // Запуск в фоновом режиме
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnShutdownRequested;
            DisableAvaloniaDataAnnotationValidation();
        
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
        
            // НЕ создаем новый DataContext - он уже создан в конструкторе MainWindow
            // mainWindow.DataContext = new MainWindowViewModel();
        
            desktop.MainWindow.Icon = new WindowIcon(
                AssetLoader.Open(new Uri("avares://BattleShipGame/Assets/BattleShipGame.ico"))
            );
        
            desktop.MainWindow.Show();
        
            // Получаем ViewModel из существующего DataContext
            if (mainWindow.DataContext is MainWindowViewModel viewModel)
            {
                viewModel.ShowLoadingScreen();
                _ = viewModel.SimulateLoadingAsync().ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => viewModel.ShowMainMenu());
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Отключает валидацию DataAnnotations в Avalonia для совместимости.
    /// </summary>
    /// <remarks>
    /// Это необходимо для предотвращения конфликтов с CommunityToolkit.Mvvm.
    /// </remarks>
    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
    
    /// <summary>
    /// Обработчик события запроса завершения работы приложения.
    /// Останавливает игровой сервер перед завершением.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события завершения работы.</param>
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _gameServer?.Stop();
    }
}