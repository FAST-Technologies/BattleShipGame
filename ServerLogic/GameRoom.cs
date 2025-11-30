using System;

namespace BattleShipGame2.ServerLogic;

public class GameRoom
{
    public string Id { get; } // ID Игровой Комнаты
    public PlayerConnection Player1 { get; } // Игрок 1
    public PlayerConnection Player2 { get; } // Игрок 2
    public bool GameStarted { get; set; } = false; // Флаг начала игры
    public bool GameOver { get; set; } = false; // Флаг окончания игры

    public GameRoom(PlayerConnection p1, PlayerConnection p2)
    {
        Id = Guid.NewGuid().ToString();
        Player1 = p1;
        Player2 = p2;
    }
}