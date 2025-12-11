using System.Collections.Generic;

namespace BattleShipGame.Models2;

/// <summary>
/// Представляет корабль в игре Морской Бой.
/// </summary>
public class Ship
{
    #region Поля и свойства
    /// <summary>
    /// Размер корабля (количество палуб).
    /// </summary>
    public int Size { get; set; }
    /// <summary>
    /// Список позиций корабля на поле.
    /// </summary>
    public List<(int X, int Y)> Positions { get; set; }
    /// <summary>
    /// Количество попаданий по кораблю.
    /// </summary>
    public int HitCount { get; set; }
    /// <summary>
    /// Ориентация корабля (true - горизонтальный, false - вертикальный).
    /// </summary>
    public bool IsHorizontal { get; set; }
    /// <summary>
    /// Флаг, указывающий потоплен ли корабль.
    /// </summary>
    public bool IsSunk => HitCount >= Size;

    /// <summary>
    /// Создает новый корабль.
    /// </summary>
    /// <param name="size">Размер корабля.</param>
    /// <param name="horizontal">Ориентация корабля.</param>
    public Ship(int size, bool horizontal)
    {
        Size = size;
        IsHorizontal = horizontal;
        Positions = new List<(int, int)>();
        HitCount = 0;
    }
    #endregion
}