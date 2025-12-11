using Avalonia.Controls;

namespace BattleShipGame.Views;

/// <summary>
/// Перечисление возможных результатов после завершения игры в локальном режиме.
/// </summary>
public enum GameOverResult
{
    /// <summary>Начать новую игру.</summary>
    NewGame,
    /// <summary>Вернуться в главное меню.</summary>
    MainMenu
}

/// <summary>
/// Диалоговое окно, отображаемое при завершении игры в локальном режиме.
/// Показывает результат игры и предоставляет опции для дальнейших действий.
/// </summary>
public partial class GameOverWindow : Window
{
    /// <summary>
    /// Получает выбранный пользователем результат.
    /// </summary>
    public GameOverResult? Result { get; private set; }
    
    /// <summary>
    /// Получает или устанавливает флаг победы текущего игрока.
    /// </summary>
    public bool IsWin { get; set; }
    
    /// <summary>
    /// Получает или устанавливает имя победителя.
    /// </summary>
    public string WinnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Инициализирует новый экземпляр класса GameOverWindow.
    /// Настраивает текст результата и обработчики событий кнопок.
    /// </summary>
    public GameOverWindow()
    {
        InitializeComponent();
        var resultText = GameOverResultText;
        var winnerText = GameOverWinnerText;
        var newGameButton = GameOverNewGameButton;
        var menuButton = GameOverMenuButton;
        
        Opened += (s, e) =>
        {
            resultText.Text = IsWin ? "🎉 ПОБЕДА! 🎉" : "💀 ПОРАЖЕНИЕ 💀";
            winnerText.Text = IsWin ? "Вы потопили весь флот противника!" : $"Победитель: {WinnerName}";
        };
        
        // Обработчики кнопок
        newGameButton.Click += (s, e) => 
        {
            Result = GameOverResult.NewGame;
            Close();
        };
        menuButton.Click += (s, e) => 
        {
            Result = GameOverResult.MainMenu;
            Close();
        };
    }
}