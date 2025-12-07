using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BattleShipGame2.Networking;

namespace BattleShipGame2.Views;

public partial class ChatControl : UserControl
{
    private List<(string sender, string message, DateTime timestamp)> _messages = new();
    private ChatManager? _chatManager;

    public ChatControl()
    {
        InitializeComponent();
        InitializeComponents();
    }
    
    private void InitializeComponents()
    {
        // Убедимся, что элементы найдены
        var messagesPanel = ChatMessagesPanel;
        if (messagesPanel == null)
            Console.WriteLine("[ChatControl] WARNING: ChatMessagesPanel not found during initialization!");
        else
            Console.WriteLine("[ChatControl] ChatMessagesPanel found successfully");
    }

    /// <summary>
    /// Устанавливает ChatManager для взаимодействия
    /// </summary>
    public void SetChatManager(ChatManager chatManager)
    {
        _chatManager = chatManager;
        _chatManager?.MessageAdded += OnMessageAdded;
    }

    /// <summary>
    /// Обработчик добавления нового сообщения
    /// </summary>
    private void OnMessageAdded(string sender, string text, DateTime timestamp)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddMessageToUI(sender, text, timestamp);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Добавляет сообщение в UI
    /// </summary>
    private void AddMessageToUI(string sender, string text, DateTime timestamp)
    {
        var messagesPanel = ChatMessagesPanel;
        if (messagesPanel == null) return;

        // Контейнер сообщения
        var messageContainer = new StackPanel();
        messageContainer.Classes.Add("ChatMessageContainer");

        // Заголовок (отправитель + время)
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

        // Текст сообщения
        var messageBlock = new TextBlock
        {
            Text = text
        };
        messageBlock.Classes.Add("ChatMessage");

        messageContainer.Children.Add(headerPanel);
        messageContainer.Children.Add(messageBlock);

        // Разделитель
        var separator = new Border();
        separator.Classes.Add("ChatSeparator");

        messagesPanel.Children.Add(messageContainer);
        messagesPanel.Children.Add(separator);

        // Прокрутка вниз
        var scrollViewer = ChatScrollViewer;
        scrollViewer?.ScrollToEnd();
    }

    /// <summary>
    /// Обработчик нажатия Enter в поле ввода
    /// </summary>
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
    /// Обработчик кнопки "Отправить"
    /// </summary>
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
    /// Форматирует время сообщения
    /// </summary>
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
    /// Очищает чат
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        var messagesPanel = ChatMessagesPanel;
        messagesPanel?.Children.Clear();
    }
}