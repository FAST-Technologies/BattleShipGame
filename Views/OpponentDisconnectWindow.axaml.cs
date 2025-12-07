using Avalonia.Controls;

namespace BattleShipGame2.Views;

public partial class OpponentDisconnectWindow : Window
{
    public string Message { get; set; } = string.Empty;
    
    public OpponentDisconnectWindow()
    {
        InitializeComponent();
        
        // Находим элементы
        var messageText = OpponentDisconnectMessage;
        var okButton = OpponentDisconnectOkButton;
        
        // Инициализация текста
        Opened += (s, e) =>
        {
            messageText.Text = Message;
        };
        
        // Обработчики кнопок
        okButton?.Click += (s, e) => Close(true);
    }
}