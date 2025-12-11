using System.Collections.Generic;
using System.Linq;

namespace BattleShipGame.ServerLogic;

/// <summary>
/// Представляет сообщение, отправляемое клиенту.
/// </summary>
/// <remarks>
/// Сериализуется в строковый формат: "TYPE:key1=value1;key2=value2\n".
/// </remarks>
public class ServerMessage
{
    /// <summary>
    /// Тип сообщения (команда).
    /// </summary>
    public required string Type { get; set; }
    
    /// <summary>
    /// Данные сообщения в формате ключ-значение.
    /// </summary>
    public Dictionary<string, string> Data { get; set; } = new();

    /// <summary>
    /// Сериализует сообщение в строковый формат протокола.
    /// </summary>
    /// <returns>Строка в формате "TYPE:key1=value1;key2=value2\n".</returns>
    public override string ToString()
    {
        var parts = new List<string> { Type };
        var dataParts = Data.Select(kvp => $"{kvp.Key}={kvp.Value}");
        parts.AddRange(dataParts);
        return string.Join(":", parts[0], string.Join(";", parts.Skip(1))) + "\n";
    }
}