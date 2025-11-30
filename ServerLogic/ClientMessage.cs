using System.Collections.Generic;

namespace BattleShipGame2.ServerLogic;

// Сообщение от клиента
public class ClientMessage
{
    public string Type { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}