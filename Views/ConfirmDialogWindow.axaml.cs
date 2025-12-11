using Avalonia.Controls;

namespace BattleShipGame.Views;

/// <summary>
/// Диалоговое окно подтверждения действия с выбором Да/Нет.
/// Используется для подтверждения критических действий (выход из игры, новая игра и т.д.).
/// </summary>
/// <remarks>
/// Возвращает boolean результат: true для подтверждения, false для отмены.
/// </remarks>
public partial class ConfirmDialogWindow : Window
{
    private TextBlock _messageText; /// <summary>Текст сообщения.</summary>
    
    /// <summary>
    /// Сообщение, отображаемое в диалоговом окне.
    /// </summary>
    public string Message 
    { 
        get => _messageText.Text ?? string.Empty;
        set 
        { 
            _messageText.Text = value; 
        }
    }
    
    /// <summary>
    /// Инициализирует новый экземпляр класса ConfirmDialogWindow.
    /// Настраивает обработчики событий для кнопок подтверждения и отмены.
    /// </summary>
    public ConfirmDialogWindow()
    {
        InitializeComponent();
        _messageText = ConfirmDialogMessage;
        var yesButton = ConfirmYesButton;
        var noButton = ConfirmNoButton;
        yesButton.Click += (_, _) => Close(true);
        noButton.Click += (_, _) => Close(false);
    }
}