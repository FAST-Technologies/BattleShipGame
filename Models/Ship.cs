using System.Collections.Generic;

namespace BattleShipGame.Models;

public class Ship
{
    public int Size { get; set; }
    public List<(int X, int Y)> Positions { get; set; }
    public int HitCount { get; set; }
    public bool IsHorizontal { get; set; }
    public bool IsSunk => HitCount >= Size;

    public Ship(int size, bool horizontal)
    {
        Size = size;
        IsHorizontal = horizontal;
        Positions = new List<(int, int)>();
        HitCount = 0;
    }
}