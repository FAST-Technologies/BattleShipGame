using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BattleShipGame.ViewModels;

/// <summary>
/// ViewModel для главного окна игры "Морской бой".
/// Управляет состоянием UI, видимостью экранов и игровой логикой.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>Действие для обнуления игрового состояния.</summary>
    public event Action? RequestGameReset;
    #region Свойства видимости экранов
    
    /// <summary>Приветственное сообщение.</summary>
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
    
    /// <summary>Флаг видимости экрана загрузки.</summary>
    [ObservableProperty]
    private bool _isLoadingScreenVisible = false;
    
    /// <summary>Флаг видимости главного меню.</summary>
    [ObservableProperty]
    private bool _isMainMenuVisible = false;
    
    /// <summary>Флаг видимости экрана расстановки кораблей.</summary>
    [ObservableProperty]
    private bool _isPlacementScreenVisible = false;
    
    /// <summary>Флаг видимости игрового экрана.</summary>
    [ObservableProperty]
    private bool _isGameScreenVisible = false;
    
    #endregion
    
    #region Текстовые свойства статусов
    
    /// <summary>Текущий статус загрузки.</summary>
    [ObservableProperty]
    private string _loadingStatus = "Инициализация...";
    
    /// <summary>Текущий статус игры.</summary>
    [ObservableProperty]
    private string _gameStatus = "⚔️ ВАШ ХОД! Атакуйте поле противника";
    
    /// <summary>Статус расстановки кораблей.</summary>
    [ObservableProperty]
    private string _placementStatus = "🚢 Расставьте корабли";
    
    /// <summary>Инструкции по расстановке кораблей.</summary>
    [ObservableProperty]
    private string _placementInstruction = "Размещаем корабль размером X клеток\nПробел - повернуть, ЛКМ - разместить";
    
    #endregion
    
    #region Свойства прогресса и состояния UI
    
    /// <summary>Прогресс загрузки от 0 до 100.</summary>
    [ObservableProperty]
    private double _loadingProgress = 0;
    
    /// <summary>Флаг доступности кнопки начала игры.</summary>
    [ObservableProperty]
    private bool _isStartGameButtonEnabled = false;
    
    #endregion
    
    #region Свойства статистики и заголовков
    
    /// <summary>Статистика выстрелов игрока.</summary>
    [ObservableProperty]
    private string _playerStats = "🎯 Ваши выстрелы: 0 попаданий, 0 промахов";
    
    /// <summary>Статистика выстрелов противника.</summary>
    [ObservableProperty]
    private string _opponentStats = "💣 Выстрелы противника: 0 попаданий, 0 промахов";
    
    /// <summary>Заголовок собственного поля.</summary>
    [ObservableProperty]
    private string _ownBoardTitle = "🛡️ ВАШЕ ПОЛЕ";
    
    /// <summary>Заголовок поля противника.</summary>
    [ObservableProperty]
    private string _enemyBoardTitle = "🎯 ПОЛЕ ПРОТИВНИКА";
    
    #endregion
    
    #region Команды
    
    /// <summary>
    /// Команда для показа главного меню.
    /// Скрывает все экраны и показывает главное меню.
    /// </summary>
    [RelayCommand]
    public void ShowMainMenu()
    {
        Console.WriteLine($"[DEBUG ViewModel] ShowMainMenu called");
        Console.WriteLine($"[DEBUG ViewModel] Before: IsLoading={IsLoadingScreenVisible}, IsMainMenu={IsMainMenuVisible}, IsPlacement={IsPlacementScreenVisible}, IsGame={IsGameScreenVisible}");
        RequestGameReset?.Invoke();
        HideAllScreens();
        IsMainMenuVisible = true;
        ResetUiState();
    
        Console.WriteLine($"[DEBUG ViewModel] After: IsMainMenuVisible={IsMainMenuVisible}");
    }
    
    /// <summary>
    /// Команда для сброса игрового состояния.
    /// Сбрасывает статусы на значения по умолчанию.
    /// </summary>
    private void ResetUiState()
    {
        GameStatus = "⚔️ ВАШ ХОД! Атакуйте поле противника";
        PlacementStatus = "🚢 Расставьте корабли";
        PlayerStats = "🎯 Ваши выстрелы: 0 попаданий, 0 промахов";
        OpponentStats = "💣 Выстрелы противника: 0 попаданий, 0 промахов";
        IsStartGameButtonEnabled = false;
    }
    
    /// <summary>
    /// Команда для показа экрана загрузки.
    /// Скрывает все экраны и показывает экран загрузки.
    /// </summary>
    [RelayCommand]
    public void ShowLoadingScreen()
    {
        HideAllScreens();
        IsLoadingScreenVisible = true;
    }
    
    /// <summary>
    /// Команда для показа экрана расстановки кораблей.
    /// Скрывает все экраны и показывает экран расстановки.
    /// </summary>
    [RelayCommand]
    public void ShowPlacementScreen()
    {
        HideAllScreens();
        IsPlacementScreenVisible = true;
        PlacementStatus = "🚢 Игрок: Расставьте корабли";
    }
    
    /// <summary>
    /// Команда для показа игрового экрана.
    /// Скрывает все экраны и показывает игровой экран.
    /// </summary>
    [RelayCommand]
    public void ShowGameScreen()
    {
        HideAllScreens();
        IsGameScreenVisible = true;
    }
    
    #endregion
    
    #region Вспомогательные методы
    
    /// <summary>
    /// Скрывает все экраны.
    /// Используется для переключения между состояниями UI.
    /// </summary>
    public void HideAllScreens()
    {
        IsLoadingScreenVisible = false;
        IsMainMenuVisible = false;
        IsPlacementScreenVisible = false;
        IsGameScreenVisible = false;
    }
    
    /// <summary>
    /// Имитирует процесс загрузки приложения.
    /// Обновляет статус и прогресс загрузки с анимацией.
    /// </summary>
    /// <returns>Задача, представляющая асинхронную операцию.</returns>
    public async Task SimulateLoadingAsync()
    {
        var loadingSteps = new[]
        {
            ("Загрузка ресурсов...", 20),
            ("Инициализация графики...", 40),
            ("Подготовка игровых досок...", 60),
            ("Загрузка звуков...", 80),
            ("Финализация...", 100)
        };

        foreach (var (status, progress) in loadingSteps)
        {
            LoadingStatus = status;
            
            var targetProgress = (400.0 - 4) * progress / 100;
            var currentProgress = LoadingProgress;
            var steps = 20;
            var increment = (targetProgress - currentProgress) / steps;

            // Плавная анимация прогресса
            for (int i = 0; i < steps; i++)
            {
                LoadingProgress = currentProgress + increment * (i + 1);
                await Task.Delay(30);
            }

            await Task.Delay(200);
        }

        LoadingStatus = "Готово! ✔";
        await Task.Delay(300);
    }
    
    #endregion
}
