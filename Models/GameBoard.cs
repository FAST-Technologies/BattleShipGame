using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleShipGame.Models;
public class GameBoard
{
    public CellState[,] Grid { get; private set; }
    public List<Ship> Ships { get; private set; }
    public int Size { get; private set; }

    public GameBoard(int size = 10)
    {
        Size = size;
        Grid = new CellState[size, size];
        Ships = new List<Ship>();
    }

    public bool PlaceShip(Ship ship, int x, int y)
    {
        if (!CanPlaceShip(x, y, ship.Size, ship.IsHorizontal))
            return false;

        for (int i = 0; i < ship.Size; i++)
        {
            int px = ship.IsHorizontal ? x + i : x;
            int py = ship.IsHorizontal ? y : y + i;
            Grid[px, py] = CellState.Ship;
            ship.Positions.Add((px, py));
        }

        Ships.Add(ship);
        return true;
    }

    private bool CanPlaceShip(int x, int y, int size, bool horizontal)
    {
        for (int i = 0; i < size; i++)
        {
            int px = horizontal ? x + i : x;
            int py = horizontal ? y : y + i;

            if (px >= Size || py >= Size)
                return false;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int checkX = px + dx;
                    int checkY = py + dy;

                    if (checkX >= 0 && checkX < Size && checkY >= 0 && checkY < Size)
                    {
                        if (Grid[checkX, checkY] == CellState.Ship)
                            return false;
                    }
                }
            }
        }
        return true;
    }

    public void PlaceShipsRandomly()
    {
        var random = new Random();
        int[] shipSizes = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };

        foreach (var size in shipSizes)
        {
            bool placed = false;
            int attempts = 0;

            while (!placed && attempts < 1000)
            {
                int x = random.Next(Size);
                int y = random.Next(Size);
                bool horizontal = random.Next(2) == 0;

                var ship = new Ship(size, horizontal);
                placed = PlaceShip(ship, x, y);
                attempts++;
            }
        }
    }

    public (bool hit, bool sunk, bool gameOver) Attack(int x, int y)
    {
        if (Grid[x, y] == CellState.Hit || Grid[x, y] == CellState.Miss || Grid[x, y] == CellState.Sunk)
            return (false, false, false);

        if (Grid[x, y] == CellState.Ship)
        {
            Grid[x, y] = CellState.Hit;

            var ship = Ships.FirstOrDefault(s => s.Positions.Contains((x, y)));
            if (ship != null)
            {
                ship.HitCount++;

                if (ship.IsSunk)
                {
                    foreach (var pos in ship.Positions)
                    {
                        Grid[pos.X, pos.Y] = CellState.Sunk;
                    }

                    bool gameOver = Ships.All(s => s.IsSunk);
                    return (true, true, gameOver);
                }
            }

            return (true, false, false);
        }
        else
        {
            Grid[x, y] = CellState.Miss;
            return (false, false, false);
        }
    }
}