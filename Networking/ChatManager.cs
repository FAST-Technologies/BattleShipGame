using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleShipGame.Views;

namespace BattleShipGame.Networking;

/// <summary>
/// Класс <c>ChatManager</c> управляет обменом текстовыми сообщениями между игроками в сетевой игре.
/// Он интегрируется с сетевым клиентом и UI-компонентом чата (<c>ChatControl</c>).
/// </summary>
public class ChatManager
{
    #region Поля и свойства
    private readonly NetworkClient _networkClient;
    private string _playerName;
    private ChatControl? _chatControl;

    /// <summary>
    /// Событие, вызываемое при добавлении нового сообщения в чат.
    /// </summary>
    /// <remarks>
    /// Параметры делегата:
    /// <list type="bullet">
    ///   <item><description><c>sender</c> — имя отправителя.</description></item>
    ///   <item><description><c>text</c> — текст сообщения.</description></item>
    ///   <item><description><c>timestamp</c> — время получения/отправки.</description></item>
    /// </list>
    /// Подписчики (обычно <c>ChatControl</c>) обновляют UI.
    /// </remarks>
    public event Action<string, string, DateTime>? MessageAdded;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatManager"/>.
    /// </summary>
    /// <param name="networkClient">Сетевой клиент для отправки сообщений.</param>
    /// <param name="playerName">Имя текущего игрока (отображается как "Вы" при отправке).</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="networkClient"/> или <paramref name="playerName"/> равны <c>null</c> или пусты.</exception>
    public ChatManager(NetworkClient networkClient, string playerName)
    {
        _networkClient = networkClient;
        _playerName = playerName;
    }
    
    #endregion
    
    #region Основная логика
    
    /// <summary>
    /// Создаёт и инициализирует UI-компонент чата.
    /// </summary>
    /// <returns>Экземпляр <see cref="ChatControl"/>, привязанный к этому менеджеру.</returns>
    /// <remarks>
    /// Метод должен вызываться один раз при инициализации UI.
    /// </remarks>
    public ChatControl CreateChatControl()
    {
        _chatControl = new ChatControl();
        _chatControl.SetChatManager(this);
        return _chatControl;
    }

    /// <summary>
    /// Добавляет сообщение в локальную историю чата и уведомляет подписчиков.
    /// </summary>
    /// <param name="sender">Имя отправителя сообщения.</param>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="timestamp">Время отправки/получения сообщения.</param>
    /// <remarks>
    /// Не отправляет сообщение по сети — только обновляет локальный чат.
    /// Используется как для входящих, так и для исходящих сообщений.
    /// </remarks>
    private void AddMessage(string sender, string text, DateTime timestamp)
    {
        Console.WriteLine($"[ChatManager] AddMessage called: {sender}: {text}");
        MessageAdded?.Invoke(sender, text, timestamp);
    }

    /// <summary>
    /// Обрабатывает входящее сетевое сообщение чата.
    /// </summary>
    /// <param name="data">Словарь с данными сообщения, содержащий ключи из <see cref="NetworkProtocol.Keys"/>.</param>
    /// <exception cref="ArgumentNullException">Если <paramref name="data"/> равен <c>null</c>.</exception>
    /// <remarks>
    /// Извлекает поля <c>Sender</c> и <c>ChatText</c> из входящего пакета.
    /// Если отправитель не указан, используется "Opponent".
    /// </remarks>
    public void HandleChatMessage(Dictionary<string, string> data)
    {
        var sender = data.GetValueOrDefault(NetworkProtocol.Keys.Sender, "Opponent");
        var text = data.GetValueOrDefault(NetworkProtocol.Keys.ChatText, "");

        Console.WriteLine($"[Chat] {sender}: {text}");
        Console.WriteLine(MessageAdded == null
            ? $"[ChatManager] WARNING: MessageAdded event has no subscribers!"
            : $"[ChatManager] MessageAdded event has subscribers, invoking...");
        AddMessage(sender, text, DateTime.Now);
    }
    
    /// <summary>
    /// Асинхронно отправляет текстовое сообщение сопернику через сетевой клиент.
    /// </summary>
    /// <param name="text">Текст сообщения для отправки.</param>
    /// <returns>Задача, представляющая асинхронную операцию отправки.</returns>
    /// <remarks>
    /// Если текст пуст или игрок не подключён — метод возвращает управление без действия.
    /// После успешной отправки сообщение также добавляется в локальный чат с пометкой "Вы".
    /// </remarks>
    public async Task SendChatMessageAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !_networkClient.IsConnected)
            return;
    
        var chatMsg = new NetworkMessage
        {
            Type = NetworkProtocol.Commands.ChatMessage,
            Data = { { NetworkProtocol.Keys.ChatText, text } }
        };
    
        await _networkClient.SendMessageAsync(chatMsg);
        AddMessage("Вы", text, DateTime.Now);
    }

    /// <summary>
    /// Очищает историю сообщений в связанном UI-контроле.
    /// </summary>
    /// <remarks>
    /// Безопасно вызывать даже если <c>ChatControl</c> не создан.
    /// </remarks>
    public void Clear()
    {
        _chatControl?.Clear();
    }
    
    #endregion
}