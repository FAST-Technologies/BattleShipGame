using System;
using System.IO;
using System.Net.Sockets;
using BattleShipGame2.Models;

namespace BattleShipGame2.ServerLogic;

public class PlayerConnection
{
    public string Id { get; } // ID игрока
    public string Name { get; set; } // Имя игрока
    public TcpClient TcpClient { get; } // Инициализация TCP-клиента
    public StreamWriter Writer { get; } // Запись
    public StreamReader Reader { get; } // Чтение
    public bool IsReady { get; set; } = false; // Готов к игре (корабли расставлены)
    public bool IsMyTurn { get; set; } = false; // Флаг проверки моего хода
    public GameBoard Board { get; set; } // Игровая доска

    public PlayerConnection(TcpClient tcpClient)
    {
        Id = Guid.NewGuid().ToString();
        TcpClient = tcpClient;
        var stream = tcpClient.GetStream();
        Reader = new StreamReader(stream);
        Writer = new StreamWriter(stream) { AutoFlush = true };
        Board = new GameBoard();
    }

    public void Close()
    {
        Writer?.Close();
        Reader?.Close();
        TcpClient?.Close();
    }
}