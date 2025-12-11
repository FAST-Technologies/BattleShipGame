using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleShipGame.Models2;

namespace BattleShipGame.Logic;

/// <summary>
/// Представляет класс для управления ботами.
/// </summary>
public class BotManager
{
    private Dictionary<BotDifficulty, BotLogic> _bots = new(); /// <summary>Список ботов по сложности.</summary>
    private BotLogic? _currentBot; /// <summary>Текущий выбранный бот.</summary>
    
    /// <summary>
    /// Инициализация менеджера ботов.
    /// </summary>
    public BotManager()
    {
        foreach (BotDifficulty difficulty in Enum.GetValues(typeof(BotDifficulty)))
            _bots[difficulty] = new BotLogic(difficulty);
    }
    
    /// <summary>
    /// Установить сложность текущего бота.
    /// </summary>
    /// <param name="difficulty">Устанавливаемая сложность бота.</param>
    public void SetDifficulty(BotDifficulty difficulty)
    {
        _currentBot = _bots[difficulty];
    }
    
    /// <summary>
    /// Выполнить простой ход (для Easy).
    /// </summary>
    /// <param name="playerBoard">Текущая игровая доска.</param>
    /// <param name="onAttack">Callback-действие для отработки результата атаки.</param>
    /// <returns>Результат хода и нужно ли продолжать ход.</returns>
    public async Task<BotTurnResult> MakeSimpleTurn(
        GameBoard playerBoard, 
        Action<int, int, bool, bool, bool> onAttack)
    {
        _currentBot ??= _bots[BotDifficulty.Easy];
        
        return await _currentBot.MakeSimpleTurn(playerBoard, onAttack);
    }
    
    /// <summary>
    /// Выполнить умный ход (для Medium/Hard).
    /// </summary>
    /// <param name="playerBoard">Текущая игровая доска.</param>
    /// <param name="onAttack">Callback-действие для отработки результата атаки.</param>
    /// <returns>Результат хода и нужно ли продолжать ход.</returns>
    public async Task<BotTurnResult> MakeSmartTurn(
        GameBoard playerBoard, 
        Action<int, int, bool, bool, bool> onAttack)
    {
        _currentBot ??= _bots[BotDifficulty.Medium];
        
        return await _currentBot.MakeSmartTurn(playerBoard, onAttack);
    }
    
    /// <summary>
    /// Сбросить состояние всех ботов.
    /// </summary>
    public void ResetAll()
    {
        foreach (var bot in _bots.Values) bot.Reset();
    }
}