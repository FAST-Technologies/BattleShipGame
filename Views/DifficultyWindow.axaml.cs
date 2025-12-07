using Avalonia.Controls;
using BattleShipGame2.Models;

namespace BattleShipGame2.Views;

public partial class DifficultyWindow : Window
{
    public BotDifficulty? SelectedDifficulty { get; private set; }
    
    public DifficultyWindow()
    {
        InitializeComponent();
        
        // Находим кнопки
        var easyButton = EasyDifficultyButton;
        var mediumButton = MediumDifficultyButton;
        var hardButton = HardDifficultyButton;
        
        // Обработчики событий
        easyButton.Click += (s, e) => SelectDifficulty(BotDifficulty.Easy);
        mediumButton.Click += (s, e) => SelectDifficulty(BotDifficulty.Medium);
        hardButton.Click += (s, e) => SelectDifficulty(BotDifficulty.Hard);
    }
    
    private void SelectDifficulty(BotDifficulty difficulty)
    {
        SelectedDifficulty = difficulty;
        Close();
    }
}