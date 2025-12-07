using Avalonia.Controls;

namespace BattleShipGame2.Views;

public partial class ConfirmDialogWindow : Window
{
    private TextBlock messageText;
    
    public string Message 
    { 
        get => messageText?.Text ?? string.Empty;
        set 
        { 
            messageText?.Text = value; 
        }
    }
    
    public ConfirmDialogWindow()
    {
        InitializeComponent();
        
        // Находим элементы
        messageText = ConfirmDialogMessage;
        var yesButton = ConfirmYesButton;
        var noButton = ConfirmNoButton;
        
        // Обработчики кнопок
        yesButton.Click += (s, e) => Close(true);
        noButton.Click += (s, e) => Close(false);
    }
}