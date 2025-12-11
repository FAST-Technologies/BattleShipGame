namespace BattleShipGame.Networking;

/// <summary>
/// Режимы сетевой игры.
/// </summary>
public enum NetworkGameMode
{
    /// <summary>Не в сетевой игре.</summary>
    None,
    /// <summary>Поиск соперника.</summary>
    Searching,
    /// <summary>Активная сетевая игра.</summary>
    InGame
}