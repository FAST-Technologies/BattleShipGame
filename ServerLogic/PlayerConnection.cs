using System;
using System.IO;
using System.Net.Sockets;
using BattleShipGame.Models2;

namespace BattleShipGame.ServerLogic;

/// <summary>
/// Представляет подключение игрока к серверу.
/// </summary>
/// <remarks>
/// Содержит всю информацию о подключенном игроке: сетевые потоки, игровое состояние и доску.
/// </remarks>
public class PlayerConnection
{
    /// <summary>
    /// Уникальный идентификатор игрока.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Имя игрока.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// TCP-клиент для связи с игроком.
    /// </summary>
    private TcpClient TcpClient { get; }
    /// <summary>
    /// Писатель для отправки сообщений игроку.
    /// </summary>
    public StreamWriter Writer { get; }
    /// <summary>
    /// Читатель для получения сообщений от игрока.
    /// </summary>
    public StreamReader Reader { get; }
    /// <summary>
    /// Флаг готовности игрока (все корабли расставлены).
    /// </summary>
    public bool IsReady { get; set; }
    /// <summary>
    /// Флаг, указывающий является ли текущий ход игрока.
    /// </summary>
    public bool IsMyTurn { get; set; }
    /// <summary>
    /// Игровая доска игрока.
    /// </summary>
    public GameBoard Board { get; set; }

    /// <summary>
    /// Создает новое подключение игрока.
    /// </summary>
    /// <param name="tcpClient">TCP-клиент подключения.</param>
    public PlayerConnection(TcpClient tcpClient)
    {
        Id = Guid.NewGuid().ToString();
        TcpClient = tcpClient;
        var stream = tcpClient.GetStream();
        Reader = new StreamReader(stream);
        Writer = new StreamWriter(stream) { AutoFlush = true };
        Board = new GameBoard();
    }

    /// <summary>
    /// Закрывает подключение и освобождает ресурсы.
    /// </summary>
    public void Close()
    {
        Writer.Close();
        Reader.Close();
        TcpClient.Close();
    }
    
    /// <summary>
    /// Освобождает ресурсы подключения.
    /// </summary>
    public void Dispose()
    {
        Close();
    }
}