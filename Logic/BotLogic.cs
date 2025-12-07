using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BattleShipGame2.Models;

namespace BattleShipGame2.Logic;

/// <summary>
/// Представляет основную логику бота.
/// </summary>
public class BotLogic
{
    #region Поля и свойства
    
    private Random _random = new Random();  /// <summary>Генератор случайных чисел.</summary>
    private BotDifficulty _difficulty; /// <summary>Выбранная сложность бота (Лёгкая, средняя или тяжёлая).</summary>
    private List<(int x, int y)> _lastHits = new(); /// <summary>Последние координаты попаданий бота (для умного ИИ).</summary>
    private (int x, int y)? _lastHitDirection = null; /// <summary>Текущее направление поиска после попадания.</summary>
    private (int x, int y)? _initialHit = null;       /// <summary>Координаты первого попадания в текущий корабль.</summary>
    
    #endregion
    
    /// <summary>
    /// Инициализация бота с некоторой сложностью.
    /// </summary>
    /// <param name="difficulty">Выбранная сложность бота.</param>
    public BotLogic(BotDifficulty difficulty)
    {
        _difficulty = difficulty;
    }
    
    #region Основные методы для работы ботов различной сложности
    
    /// <summary>
    /// Простой ход бота (только рандом).
    /// </summary>
    /// <param name="playerBoard">Доска игрока, по которой бот стреляет.</param>
    /// <param name="onAttack">Callback-действие для отработки результата атаки.</param>
    /// <returns>Результат хода и нужно ли продолжать ход.</returns>
    public async Task<BotTurnResult> MakeSimpleTurn(GameBoard playerBoard, Action<int, int, bool, bool, bool> onAttack)
    {
        var possibleTargets = GetSmartTargets(playerBoard);
        
        if (possibleTargets.Count == 0)
            return new BotTurnResult(false, false, false);

        int randomIndex = _random.Next(possibleTargets.Count);
        var (x, y) = possibleTargets[randomIndex];
        possibleTargets.RemoveAt(randomIndex);

        var (hit, sunk, gameOver) = playerBoard.Attack(x, y);

        // Вызываем колбэк для обработки результатов
        onAttack?.Invoke(x, y, hit, sunk, gameOver);
        
        // Определяем, продолжать ли ход
        bool continueTurn = hit && !gameOver;
        bool isPlayerTurn = !continueTurn && !gameOver;
        
        return new BotTurnResult(continueTurn, isPlayerTurn, gameOver);
    }

    /// <summary>
    /// Умный ход бота (с анализом попаданий).
    /// </summary>
    /// <param name="playerBoard">Доска игрока, по которой бот стреляет.</param>
    /// <param name="onAttack">Callback-действие для отработки результата атаки.</param>
    /// <returns>Результат хода и нужно ли продолжать ход.</returns>
    public async Task<BotTurnResult> MakeSmartTurn(GameBoard playerBoard, Action<int, int, bool, bool, bool> onAttack)
    {
        var possibleTargets = GetSmartTargets(playerBoard);
        
        if (possibleTargets.Count == 0)
            return new BotTurnResult(false, false, false);

        var target = GetNextSmartShot(possibleTargets, playerBoard);
        possibleTargets.Remove((target.x, target.y));

        var (hit, sunk, gameOver) = playerBoard.Attack(target.x, target.y);
        
        // Сохраняем информацию о попадании для умного ИИ
        if (hit)
        {
            _lastHits.Add((target.x, target.y));
            if (_lastHits.Count > 5) _lastHits.RemoveAt(0);
            
            _initialHit ??= (target.x, target.y);
            
            // Определяем направление при втором попадании
            if (_lastHits.Count >= 2)
            {
                var last = _lastHits[^1];
                var prev = _lastHits[^2];
                _lastHitDirection = (last.x - prev.x, last.y - prev.y);
            }
            
            if (sunk)
            {
                // Сбрасываем информацию при потоплении
                _lastHits.Clear();
                _lastHitDirection = null;
                _initialHit = null;
            }
        }

        // Вызываем колбэк для обработки результатов
        onAttack?.Invoke(target.x, target.y, hit, sunk, gameOver);
        
        // Определяем, продолжать ли ход
        bool continueTurn = hit && !gameOver;
        bool isPlayerTurn = !continueTurn && !gameOver;
        
        return new BotTurnResult(continueTurn, isPlayerTurn, gameOver);
    }
    
    /// <summary>
    /// Возвращает список всех ещё не обстрелянных клеток (для бота).
    /// </summary>
    /// <param name="board">Текущая игровая клетка с доступными и ограниченными клетками.</param>
    /// <returns>Список координат.</returns>
    private List<(int x, int y)> GetSmartTargets(GameBoard board)
    {
        var targets = new List<(int x, int y)>();
        for (int x = 0; x < board.Size; x++)
            for (int y = 0; y < board.Size; y++)
                if (board.Grid[x, y] == CellState.Empty || board.Grid[x, y] == CellState.Ship)
                    targets.Add((x, y));
        return targets;
    }

    /// <summary>
    /// Выбирает следующую цель для выстрела с учётом сложности бота.
    /// Hard — следует по линии попаданий, Medium — бьёт по соседям, Easy — рандом.
    /// </summary>
    /// <param name="possibleTargets">Список доступных клеток.</param>
    /// <param name="board">Текущая игровая доска.</param>
    /// <returns>Выбранные координаты для атаки.</returns>
    private (int x, int y) GetNextSmartShot(List<(int x, int y)> possibleTargets, GameBoard board)
    {
        switch (_difficulty)
        {
            // === СЛОЖНЫЙ ИИ: стреляем рядом с попаданиями ===
            case BotDifficulty.Hard:
                return GetSmartTargetHard(possibleTargets, board);
            
            // === СРЕДНИЙ: приоритет соседям попаданий ===
            case BotDifficulty.Medium:
                return GetSmartTargetMedium(possibleTargets, board);
            
            // === ЛЁГКИЙ: рандом ===    
            case BotDifficulty.Easy:
            default:
                return GetRandomTarget(possibleTargets);
        }
    }
    
    /// <summary>
    /// Сложный уровень атаки: продвинутая логика.
    /// </summary>
    /// <param name="possibleTargets">Список доступных клеток.</param>
    /// <param name="board">Текущая игровая доска.</param>
    /// <returns>Выбранные координаты для атаки.</returns>
    private (int x, int y) GetSmartTargetHard(List<(int x, int y)> possibleTargets, GameBoard board)
    {
        if (_lastHits.Count == 0)
            return GetRandomTarget(possibleTargets);

        var lastHit = _lastHits.Last();

        // 1. Продолжаем в том же направлении
        if (_lastHitDirection.HasValue && _lastHits.Count >= 2)
        {
            var (dx, dy) = _lastHitDirection.Value;
            int nextX = lastHit.x + dx;
            int nextY = lastHit.y + dy;

            if (IsValidAndAvailable(nextX, nextY, possibleTargets, board))
                return (nextX, nextY);
        }

        // 2. Пробуем противоположное направление
        if (_lastHitDirection.HasValue && _initialHit.HasValue)
        {
            var (dx, dy) = _lastHitDirection.Value;
            int oppositeX = _initialHit.Value.x - dx;
            int oppositeY = _initialHit.Value.y - dy;

            if (IsValidAndAvailable(oppositeX, oppositeY, possibleTargets, board))
                return (oppositeX, oppositeY);
        }

        // 3. Стреляем по соседям последнего попадания
        var neighbors = GetNeighbors(lastHit.x, lastHit.y, board)
            .Where(neighbor => possibleTargets.Contains(neighbor))
            .ToList();

        if (neighbors.Count > 0)
            return neighbors[_random.Next(neighbors.Count)];

        return GetRandomTarget(possibleTargets);
    }
    
    /// <summary>
    /// Средний уровень: стреляет по соседям попаданий.
    /// </summary>
    /// <param name="possibleTargets">Список доступных клеток.</param>
    /// <param name="board">Текущая игровая доска.</param>
    /// <returns>Выбранные координаты для атаки.</returns>
    private (int x, int y) GetSmartTargetMedium(List<(int x, int y)> possibleTargets, GameBoard board)
    {
        if (_lastHits.Count == 0)
            return GetRandomTarget(possibleTargets);

        // Собираем всех соседей всех попаданий
        var allNeighbors = new List<(int x, int y)>();
        foreach (var hit in _lastHits)
            allNeighbors.AddRange(GetNeighbors(hit.x, hit.y, board));

        // Убираем дубликаты и выбираем только доступные цели
        var uniqueNeighbors = allNeighbors
            .Distinct()
            .Where(neighbor => possibleTargets.Contains(neighbor))
            .ToList();

        if (uniqueNeighbors.Count > 0)
            return uniqueNeighbors[_random.Next(uniqueNeighbors.Count)];

        return GetRandomTarget(possibleTargets);
    }
    
    /// <summary>
    /// Лёгкий уровень: случайный выбор.
    /// </summary>
    /// <param name="possibleTargets">Список доступных клеток.</param>
    /// <returns>Выбранные координаты для атаки.</returns>
    private (int x, int y) GetRandomTarget(List<(int x, int y)> possibleTargets)
    {
        return possibleTargets[_random.Next(possibleTargets.Count)];
    }
    
    #endregion
    
    #region Вспомогательные методы

    /// <summary>
    /// Проверяет, валидна ли координата и доступна ли для выстрела.
    /// </summary>
    /// <param name="x">Координата x клетки.</param>
    /// <param name="y">Координата y клетки.</param>
    /// <param name="targets">Список доступных клеток.</param>
    /// <param name="board">Текущая игровая доска.</param>
    /// <returns>Возвращает флаг валидности и доступности клетки.</returns>
    private bool IsValidAndAvailable(int x, int y, List<(int x, int y)> targets, GameBoard board)
    {
        return x >= 0 && x < board.Size && y >= 0 && y < board.Size &&
               (board.Grid[x, y] == CellState.Empty || board.Grid[x, y] == CellState.Ship) &&
               targets.Contains((x, y));
    }

    /// <summary>
    /// Возвращает список соседних клеток (вверх, вниз, влево, вправо).
    /// </summary>
    /// <param name="x">Координата X центра.</param>
    /// <param name="y">Координата Y центра.</param>
    /// <param name="board">Текущая игровая доска.</param>
    /// <returns>Список до 4 соседних координат.</returns>
    private List<(int x, int y)> GetNeighbors(int x, int y, GameBoard board)
    {
        var neighbors = new List<(int x, int y)>();
        int[][] directions = { [-1, 0], [1, 0], [0, -1], [0, 1] }; // 4 стороны

        foreach (var dir in directions)
        {
            int nx = x + dir[0];
            int ny = y + dir[1];
            if (nx >= 0 && nx < board.Size && ny  >= 0 && ny < board.Size)
                neighbors.Add((nx, ny));
        }

        return neighbors;
    }
    
    #endregion

    /// <summary>
    /// Сброс состояния бота (например, при новой игре).
    /// </summary>
    public void Reset()
    {
        _lastHits.Clear();
        _lastHitDirection = null;
        _initialHit = null;
    }
}

/// <summary>
/// Результат хода бота.
/// </summary>
public class BotTurnResult
{
    #region Поля и свойства
    /// <summary>Флаг возможности продолжения хода.</summary>
    public bool ContinueTurn { get; } 
    /// <summary>Флаг проверки хода игрока.</summary>
    public bool IsPlayerTurn { get; }
    /// <summary>Флаг окончания игры.</summary>
    public bool GameOver { get; }
    #endregion

    /// <summary>
    /// Возвращает результат атаки бота выбранной сложности.
    /// </summary>
    /// <param name="continueTurn">Возможность продолжения хода.</param>
    /// <param name="isPlayerTurn">Проверка хода игрока.</param>
    /// <param name="gameOver">Флаг окончания игры.</param>
    public BotTurnResult(bool continueTurn, bool isPlayerTurn, bool gameOver)
    {
        ContinueTurn = continueTurn;
        IsPlayerTurn = isPlayerTurn;
        GameOver = gameOver;
    }
}