using System.Collections.Generic;

namespace BattleShipGame.ServerLogic;

/// <summary>
/// Представляет сообщение, полученное от клиента.
/// </summary>
/// <remarks>
/// Используется для десериализации входящих сообщений от клиентов.
/// Соответствует формату NetworkMessage на стороне клиента.
/// </remarks>
public class ClientMessage
{
    /// <summary>
    /// Тип сообщения (команда).
    /// </summary>
    public required string Type { get; set; }
    /// <summary>
    /// Данные сообщения в формате ключ-значение.
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = new();
}