using Avalonia.Controls;
using BattleShipGame.Models2;

namespace BattleShipGame.Views;

/// <summary>
/// Окно выбора уровня сложности бота для режима игры против компьютера.
/// Предоставляет выбор между тремя уровнями сложности: Легкий, Средний, Сложный.
/// </summary>
/// <remarks>
/// Используется только в режиме игры против компьютера (GameMode.VsComputer).
/// </remarks>
public partial class DifficultyWindow : Window
{
    /// <summary>
    /// Получает выбранный уровень сложности бота.
    /// </summary>
    /// <value>Выбранный уровень сложности или null, если выбор не был сделан.</value>
    public BotDifficulty? SelectedDifficulty { get; private set; }
    
    /// <summary>
    /// Инициализирует новый экземпляр класса DifficultyWindow.
    /// Настраивает обработчики событий для кнопок выбора сложности.
    /// </summary>
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
    
    /// <summary>
    /// Выбирает указанный уровень сложности и закрывает окно.
    /// </summary>
    /// <param name="difficulty">Уровень сложности для выбора.</param>
    private void SelectDifficulty(BotDifficulty difficulty)
    {
        SelectedDifficulty = difficulty;
        Close();
    }
}