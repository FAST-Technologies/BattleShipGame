using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BattleShipGame.Networking;

namespace BattleShipGame.Views;

/// <summary>
/// Пользовательский контрол для отображения и управления чатом в сетевой игре.
/// Обеспечивает отправку и получение сообщений, форматирование времени и управление UI чата.
/// </summary>
/// <remarks>
/// Контрол используется только в сетевом режиме игры для общения между игроками.
/// Автоматически подключается к ChatManager для обработки сетевых сообщений.
/// </remarks>
public partial class ChatControl : UserControl
{
    private readonly List<(string sender, string message, DateTime timestamp)> _messages = new(); /// <summary>Массив сообщений чата.</summary>
    private ChatManager? _chatManager; /// <summary>Инициализация менеджера чатов.</summary>

    /// <summary>
    /// Инициализирует новый экземпляр класса ChatControl.
    /// </summary>
    public ChatControl()
    {
        InitializeComponent();
        InitializeComponents();
    }
    
    /// <summary>
    /// Инициализирует UI компоненты контрола.
    /// Выполняет проверку наличия необходимых элементов.
    /// </summary>
    private void InitializeComponents()
    {
        var messagesPanel = ChatMessagesPanel;
        Console.WriteLine(messagesPanel == null 
            ? "[ChatControl] WARNING: ChatMessagesPanel not found during initialization!" 
            : "[ChatControl] ChatMessagesPanel found successfully");
    }

    /// <summary>
    /// Устанавливает ChatManager для взаимодействия с сетью.
    /// Подписывается на события добавления сообщений.
    /// </summary>
    /// <param name="chatManager">Менеджер чата для установки соединения.</param>
    /// <remarks>
    /// Должен быть вызван перед использованием контрола для работы с сетевым чатом.
    /// </remarks>
    public void SetChatManager(ChatManager chatManager)
    {
        _chatManager = chatManager;
        _chatManager.MessageAdded += OnMessageAdded;
    }

    /// <summary>
    /// Обработчик события добавления нового сообщения от ChatManager.
    /// Обновляет UI в потоке диспетчера Avalonia.
    /// </summary>
    /// <param name="sender">Имя отправителя сообщения.</param>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="timestamp">Временная метка сообщения.</param>
    private void OnMessageAdded(string sender, string text, DateTime timestamp)
    {
        _messages.Add((sender, text, timestamp));
        Dispatcher.UIThread.Post(() =>
        {
            AddMessageToUi(sender, text, timestamp);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Добавляет новое сообщение в пользовательский интерфейс чата.
    /// Создает форматированные элементы для отображения сообщения.
    /// </summary>
    /// <param name="sender">Имя отправителя.</param>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="timestamp">Временная метка.</param>
    private void AddMessageToUi(string sender, string text, DateTime timestamp)
    {
        var messagesPanel = ChatMessagesPanel;
        if (messagesPanel == null) return;
        
        var messageContainer = new StackPanel();
        messageContainer.Classes.Add("ChatMessageContainer");
        
        var headerPanel = new StackPanel();
        headerPanel.Classes.Add("ChatMessageHeader");

        var senderBlock = new TextBlock
        {
            Text = sender
        };
        senderBlock.Classes.Add("ChatSender");
        senderBlock.Classes.Add(sender == "Вы" ? "ChatSenderYou" : "ChatSenderOther");

        var timeBlock = new TextBlock
        {
            Text = FormatTimestamp(timestamp)
        };
        timeBlock.Classes.Add("ChatTimestamp");

        headerPanel.Children.Add(senderBlock);
        headerPanel.Children.Add(timeBlock);
        
        var messageBlock = new TextBlock
        {
            Text = text
        };
        messageBlock.Classes.Add("ChatMessage");

        messageContainer.Children.Add(headerPanel);
        messageContainer.Children.Add(messageBlock);
        
        var separator = new Border();
        separator.Classes.Add("ChatSeparator");

        messagesPanel.Children.Add(messageContainer);
        messagesPanel.Children.Add(separator);
        
        var scrollViewer = ChatScrollViewer;
        scrollViewer?.ScrollToEnd();
    }

    /// <summary>
    /// Обработчик нажатия клавиши Enter в поле ввода сообщения.
    /// Отправляет сообщение и очищает поле ввода.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события клавиши.</param>
    private async void OnChatInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _chatManager != null)
        {
            var inputBox = ChatInputBox;
            if (inputBox != null && !string.IsNullOrWhiteSpace(inputBox.Text))
            {
                await _chatManager.SendChatMessageAsync(inputBox.Text);
                inputBox.Text = "";
            }
        }
    }

    /// <summary>
    /// Обработчик клика по кнопке "Отправить".
    /// Отправляет сообщение и устанавливает фокус на поле ввода.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события маршрутизации.</param>
    private async void OnSendButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_chatManager != null)
        {
            var inputBox = ChatInputBox;
            if (inputBox != null && !string.IsNullOrWhiteSpace(inputBox.Text))
            {
                await _chatManager.SendChatMessageAsync(inputBox.Text);
                inputBox.Text = "";
                inputBox.Focus();
            }
        }
    }

    /// <summary>
    /// Форматирует временную метку сообщения в удобочитаемый формат.
    /// </summary>
    /// <param name="timestamp">Исходная временная метка.</param>
    /// <returns>Отформатированная строка времени.</returns>
    /// <remarks>
    /// Форматы времени:
    /// - Сегодня: "HH:mm:ss"
    /// - Вчера: "Вчера HH:mm"
    /// - В течение недели: день недели + "HH:mm"
    /// - Более недели назад: "dd.MM.yyyy HH:mm"
    /// </remarks>
    private string FormatTimestamp(DateTime timestamp)
    {
        var now = DateTime.Now;

        if (timestamp.Date == now.Date)
        {
            return timestamp.ToString("HH:mm:ss");
        }
        else if (timestamp.Date == now.Date.AddDays(-1))
        {
            return "Вчера " + timestamp.ToString("HH:mm");
        }
        else if ((now - timestamp).TotalDays < 7)
        {
            return timestamp.ToString("dddd HH:mm", new System.Globalization.CultureInfo("ru-RU"));
        }
        else
        {
            return timestamp.ToString("dd.MM.yyyy HH:mm");
        }
    }

    /// <summary>
    /// Очищает историю сообщений чата.
    /// Удаляет все сообщения из коллекции и очищает UI.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        var messagesPanel = ChatMessagesPanel;
        messagesPanel?.Children.Clear();
    }
}