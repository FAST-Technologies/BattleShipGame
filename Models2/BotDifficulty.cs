namespace BattleShipGame.Models2;

/// <summary>
/// Уровни сложности компьютерного противника.
/// </summary>
public enum BotDifficulty
{
    /// <summary>Случайные выстрелы.</summary>
    Easy,
    /// <summary>Случайные выстрелы + приоритет соседним клеткам после попадания.</summary>
    Medium,
    /// <summary>Продвинутый алгоритм с анализом вероятностей.</summary>
    Hard 
}
