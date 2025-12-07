using Avalonia.Controls;

namespace BattleShipGame2.Views;

public enum GameOverResult
{
    NewGame,
    MainMenu
}

public partial class GameOverWindow : Window
{
    public GameOverResult? Result { get; private set; }
    public bool IsWin { get; set; }
    public string WinnerName { get; set; } = string.Empty;
    
    public GameOverWindow()
    {
        InitializeComponent();
        
        // Находим элементы
        var resultText = GameOverResultText;
        var winnerText = GameOverWinnerText;
        var newGameButton = GameOverNewGameButton;
        var menuButton = GameOverMenuButton;
        
        // Инициализация текста при загрузке
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