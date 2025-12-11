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
    private TextBlock messageText; /// <summary>Текст сообщения.</summary>
    
    /// <summary>
    /// Сообщение, отображаемое в диалоговом окне.
    /// </summary>
    public string Message 
    { 
        get => messageText?.Text ?? string.Empty;
        set 
        { 
            messageText.Text = value; 
        }
    }
    
    /// <summary>
    /// Инициализирует новый экземпляр класса ConfirmDialogWindow.
    /// Настраивает обработчики событий для кнопок подтверждения и отмены.
    /// </summary>
    public ConfirmDialogWindow()
    {
        InitializeComponent();
        messageText = ConfirmDialogMessage;
        var yesButton = ConfirmYesButton;
        var noButton = ConfirmNoButton;
        yesButton.Click += (s, e) => Close(true);
        noButton.Click += (s, e) => Close(false);
    }
}