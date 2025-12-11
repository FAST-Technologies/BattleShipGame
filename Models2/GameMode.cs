namespace BattleShipGame.Models2;

/// <summary>
/// Режимы игры.
/// </summary>
public enum GameMode
{
    /// <summary>Главное меню.</summary>
    Menu,
    /// <summary>Расстановка кораблей.</summary>
    PlacingShips,
    /// <summary>Игра против компьютера.</summary>
    VsComputer,
    /// <summary>Игра против другого игрока.</summary>
    VsPlayer
}
