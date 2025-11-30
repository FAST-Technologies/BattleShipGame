using System.Collections.Generic;
using System.Linq;

namespace BattleShipGame2.ServerLogic;

// Сообщение к клиенту
public class ServerMessage
{
    public string Type { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();

    public override string ToString()
    {
        // Формируем строку вида "TYPE:KEY1=VALUE1;KEY2=VALUE2\n"
        var parts = new List<string> { Type };
        var dataParts = Data.Select(kvp => $"{kvp.Key}={kvp.Value}");
        parts.AddRange(dataParts);
        return string.Join(":", parts[0], string.Join(";", parts.Skip(1))) + "\n";
    }
}