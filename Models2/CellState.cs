namespace BattleShipGame.Models2;

/// <summary>
/// Состояния клетки игрового поля.
/// </summary>
public enum CellState
{
    /// <summary>Пустая клетка.</summary>
    Empty,
    /// <summary>Клетка с кораблем.</summary>
    Ship,
    /// <summary>Промах.</summary>
    Miss,
    /// <summary>Попадание.</summary>
    Hit,
    /// <summary>Уничтоженный корабль.</summary>
    Sunk,
    /// <summary>Заблокированная клетка (вокруг уничтоженного корабля).</summary>
    Blocked
}