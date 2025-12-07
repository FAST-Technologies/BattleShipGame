using System.Collections.Generic;

namespace BattleShipGame2.Networking;

/// <summary>
/// Представляет сетевое сообщение, передаваемое между клиентом и сервером.
/// </summary>
/// <remarks>
/// Сообщение состоит из типа (команды) и набора ключ-значение пар данных.
/// Формат сериализации: "Type:key1=value1;key2=value2".
/// </remarks>
public class NetworkMessage
{
    /// <summary>
    /// Тип сообщения (команда). Должен соответствовать NetworkProtocol.Commands.
    /// </summary>
    /// <example>"ATTACK", "GAME_START", "CHAT_MESSAGE".</example>
    public required string Type { get; set; }
    /// <summary>
    /// Словарь данных сообщения. Содержит дополнительные параметры команды.
    /// </summary>
    /// <example>
    /// Для сообщения "ATTACK" может содержать: {"x": "5", "y": "3"}.
    /// </example>
    public Dictionary<string, string> Data { get; set; } = new();
}