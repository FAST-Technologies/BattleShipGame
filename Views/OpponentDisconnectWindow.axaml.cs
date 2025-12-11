using Avalonia.Controls;

namespace BattleShipGame.Views;

/// <summary>
/// Диалоговое окно уведомления о потере соединения с соперником.
/// Используется для информирования игрока о разрыве сетевого соединения.
/// </summary>
public partial class OpponentDisconnectWindow : Window
{
    /// <summary>
    /// Получает или устанавливает сообщение для отображения.
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Инициализирует новый экземпляр класса OpponentDisconnectWindow.
    /// Настраивает отображение сообщения и обработчик кнопки подтверждения.
    /// </summary>
    public OpponentDisconnectWindow()
    {
        InitializeComponent();
        var messageText = OpponentDisconnectMessage;
        var okButton = OpponentDisconnectOkButton;
        
        Opened += (_, _) =>
        {
            messageText.Text = Message;
        };
        
        // Обработчики кнопок
        okButton.Click += (_, _) => Close(true);
    }
}