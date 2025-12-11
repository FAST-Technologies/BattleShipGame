using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleShipGame.Models2;

/// <summary>
/// Игровое поле для Морского Боя.
/// </summary>
public class GameBoard
{
    #region Поля и свойства
    /// <summary>
    /// Двумерный массив состояний клеток поля.
    /// </summary>
    public CellState[,] Grid { get; private set; }
    /// <summary>
    /// Список кораблей на поле.
    /// </summary>
    public List<Ship> Ships { get; private set; }
    /// <summary>
    /// Размер поля (обычно 10x10).
    /// </summary>
    public int Size { get; private set; }

    /// <summary>
    /// Создает новое игровое поле.
    /// </summary>
    /// <param name="size">Размер поля.</param>
    public GameBoard(int size = 10)
    {
        Size = size;
        Grid = new CellState[size, size];
        Ships = new List<Ship>();
        InitializeGrid();
    }
    #endregion
    
    #region Методы класса GameBoard
    /// <summary>
    /// Инициализирует поле пустыми клетками.
    /// </summary>
    private void InitializeGrid()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Grid[x, y] = CellState.Empty;
            }
        }
    }

    /// <summary>
    /// Размещает корабль на поле.
    /// </summary>
    /// <param name="ship">Корабль для размещения.</param>
    /// <param name="x">X-координата начала.</param>
    /// <param name="y">Y-координата начала.</param>
    /// <returns>True если корабль успешно размещен.</returns>
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

    /// <summary>
    /// Проверяет возможность размещения корабля.
    /// </summary>
    /// <param name="x">X-координата начала.</param>
    /// <param name="y">Y-координата начала.</param>
    /// <param name="size">Размер корабля.</param>
    /// <param name="horizontal">Ориентация корабля.</param>
    /// <returns>True если корабль можно разместить.</returns>
    public bool CanPlaceShip(int x, int y, int size, bool horizontal)
    {
        for (int i = 0; i < size; i++)
        {
            int px = horizontal ? x + i : x;
            int py = horizontal ? y : y + i;

            if (px >= Size || py >= Size)
                return false;

            // Проверка соседних клеток
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

    /// <summary>
    /// Автоматически расставляет корабли на поле.
    /// </summary>
    public void PlaceShipsRandomly()
    {
        var random = new Random();
        int[] shipSizes = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 }; // Стандартный набор кораблей

        foreach (var size in shipSizes)
        {
            bool placed = false;
            int attempts = 0;
            const int maxAttempts = 1000;

            while (!placed && attempts < maxAttempts)
            {
                int x = random.Next(Size);
                int y = random.Next(Size);
                bool horizontal = random.Next(2) == 0;

                var ship = new Ship(size, horizontal);
                placed = PlaceShip(ship, x, y);
                attempts++;
            }
            if (!placed)
            {
                throw new InvalidOperationException($"Не удалось разместить корабль размером {size} после {maxAttempts} попыток");
            }
        }
    }

    /// <summary>
    /// Выполняет атаку по указанным координатам.
    /// </summary>
    /// <param name="x">X-координата атаки.</param>
    /// <param name="y">Y-координата атаки.</param>
    /// <returns>Результат атаки: (попадание, потопление, конец игры).</returns>
    public (bool hit, bool sunk, bool gameOver) Attack(int x, int y)
    {
        // Проверка валидности координат
        if (x < 0 || x >= Size || y < 0 || y >= Size)
            return (false, false, false);
        
        // Проверка, что клетка еще не атакована
        if (Grid[x, y] == CellState.Hit || Grid[x, y] == CellState.Miss || 
            Grid[x, y] == CellState.Sunk || Grid[x, y] == CellState.Blocked)
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
                        Grid[pos.X, pos.Y] = CellState.Sunk;
                    BlockAroundShip(ship);

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

    /// <summary>
    /// Блокирует клетки вокруг потопленного корабля.
    /// </summary>
    /// <param name="ship">Потопленный корабль.</param>
    private void BlockAroundShip(Ship ship)
    {
        foreach (var (x, y) in ship.Positions)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int checkX = x + dx;
                    int checkY = y + dy;

                    if (checkX >= 0 && checkX < Size && checkY >= 0 && checkY < Size)
                    {
                        if (Grid[checkX, checkY] == CellState.Empty)
                        {
                            Grid[checkX, checkY] = CellState.Blocked;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Очищает поле (удаляет все корабли и сбрасывает состояния).
    /// </summary>
    public void Clear()
    {
        Grid = new CellState[Size, Size];
        Ships.Clear();
        InitializeGrid();
    }
    
    /// <summary>
    /// Получает состояние клетки по координатам.
    /// </summary>
    /// <param name="x">X-координата.</param>
    /// <param name="y">Y-координата.</param>
    /// <returns>Состояние клетки.</returns>
    public CellState GetCellState(int x, int y)
    {
        if (x >= 0 && x < Size && y >= 0 && y < Size)
            return Grid[x, y];
        
        return CellState.Empty;
    }
    
    /// <summary>
    /// Проверяет, можно ли атаковать указанную клетку
    /// </summary>
    public bool CanAttackCell(int x, int y)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size)
            return false;
    
        var state = Grid[x, y];
        return state == CellState.Empty || state == CellState.Ship;
    }
    
    #endregion
}