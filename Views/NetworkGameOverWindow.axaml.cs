using Avalonia.Controls;

namespace BattleShipGame2.Views;

public enum NetworkGameOverResult
{
    NewOnlineGame,
    MainMenu
}

public partial class NetworkGameOverWindow : Window
{
    public NetworkGameOverResult? Result { get; private set; }
    public bool IsWin { get; set; }
    public string WinnerName { get; set; } = string.Empty;
    
    public NetworkGameOverWindow()
    {
        InitializeComponent();
        
        // Находим элементы
        var resultText = NetworkGameOverResultText;
        var winnerText = NetworkGameOverWinnerText;
        var newGameButton = NetworkGameOverNewGameButton;
        var menuButton = NetworkGameOverMenuButton;
        
        // Инициализация текста
        Opened += (s, e) =>
        {
            resultText.Text = IsWin ? "🎉 ПОБЕДА! 🎉" : "💀 ПОРАЖЕНИЕ 💀";
            winnerText.Text = IsWin ? "Вы потопили весь флот противника!" : $"Победитель: {WinnerName}";
        };
        
        // Обработчики кнопок
        newGameButton.Click += (s, e) => 
        {
            Result = NetworkGameOverResult.NewOnlineGame;
            Close();
        };
        menuButton.Click += (s, e) => {
            Result = NetworkGameOverResult.MainMenu;
            Close();
        };
    }
}