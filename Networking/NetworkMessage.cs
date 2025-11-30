using System.Collections.Generic;

namespace BattleShipGame2.Networking;
public class NetworkMessage
{
    public string Type { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
}