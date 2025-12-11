using System;

namespace BattleShipGame.ServerLogic;

/// <summary>
/// Представляет игровую комнату для двух игроков.
/// </summary>
public class GameRoom
{
    /// <summary>
    /// Уникальный идентификатор комнаты.
    /// </summary>
    public string Id { get; }
    /// <summary>
    /// Первый игрок в комнате.
    /// </summary>
    public PlayerConnection Player1 { get; }
    /// <summary>
    /// Второй игрок в комнате.
    /// </summary>
    public PlayerConnection Player2 { get; }
    /// <summary>
    /// Флаг, указывающий началась ли игра.
    /// </summary>
    public bool GameStarted { get; set; }
    /// <summary>
    /// Флаг, указывающий завершилась ли игра.
    /// </summary>
    public bool GameOver { get; set; }

    /// <summary>
    /// Создает новую игровую комнату для двух игроков.
    /// </summary>
    /// <param name="p1">Первый игрок.</param>
    /// <param name="p2">Второй игрок.</param>
    public GameRoom(PlayerConnection p1, PlayerConnection p2)
    {
        Id = Guid.NewGuid().ToString();
        Player1 = p1;
        Player2 = p2;
    }
    
    /// <summary>
    /// Получает противника для указанного игрока.
    /// </summary>
    /// <param name="player">Игрок, для которого нужно найти противника.</param>
    /// <returns>Подключение противника или null если игрок не найден в комнате.</returns>
    public PlayerConnection? GetOpponent(PlayerConnection player)
    {
        if (Player1.Id == player.Id) return Player2;
        if (Player2.Id == player.Id) return Player1;
        return null;
    }

    /// <summary>
    /// Проверяет, находится ли игрок в этой комнате.
    /// </summary>
    /// <param name="player">Игрок для проверки.</param>
    /// <returns>True если игрок находится в комнате.</returns>
    public bool ContainsPlayer(PlayerConnection player)
    {
        return Player1.Id == player.Id || Player2.Id == player.Id;
    }
}