using Avalonia.Controls;

namespace BattleShipGame.Views;

/// <summary>
/// Перечисление возможных результатов после завершения сетевой игры.
/// </summary>
public enum NetworkGameOverResult
{
    /// <summary>Начать новую сетевую игру.</summary>
    NewOnlineGame,
    /// <summary>Вернуться в главное меню.</summary>
    MainMenu
}

/// <summary>
/// Диалоговое окно, отображаемое при завершении сетевой игры.
/// Показывает результат игры и предоставляет опции для дальнейших действий.
/// </summary>
/// <remarks>
/// Отличается от GameOverWindow наличием опции "Новая онлайн игра".
/// </remarks>
public partial class NetworkGameOverWindow : Window
{
    /// <summary>
    /// Получает выбранный пользователем результат.
    /// </summary>
    public NetworkGameOverResult? Result { get; private set; }
    
    /// <summary>
    /// Получает или устанавливает флаг победы текущего игрока.
    /// </summary>
    public bool IsWin { get; set; }
    
    /// <summary>
    /// Получает или устанавливает имя победителя.
    /// </summary>
    public string WinnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Инициализирует новый экземпляр класса NetworkGameOverWindow.
    /// Настраивает текст результата и обработчики событий кнопок.
    /// </summary>
    public NetworkGameOverWindow()
    {
        InitializeComponent();
        
        var resultText = NetworkGameOverResultText;
        var winnerText = NetworkGameOverWinnerText;
        var newGameButton = NetworkGameOverNewGameButton;
        var menuButton = NetworkGameOverMenuButton;
        
        Opened += (_, _) =>
        {
            resultText.Text = IsWin ? "🎉 ПОБЕДА! 🎉" : "💀 ПОРАЖЕНИЕ 💀";
            winnerText.Text = IsWin ? "Вы потопили весь флот противника!" : $"Победитель: {WinnerName}";
        };
        
        // Обработчики кнопок
        newGameButton.Click += (_, _) => 
        {
            Result = NetworkGameOverResult.NewOnlineGame;
            Close();
        };
        menuButton.Click += (_, _) => {
            Result = NetworkGameOverResult.MainMenu;
            Close();
        };
    }
}