// Networking/NetworkProtocol.cs
using System.Collections.Generic;

namespace BattleShipGame2.Networking;

/// <summary>
/// Статический класс, определяющий протокол обмена сообщениями между клиентом и сервером.
/// </summary>
public static class NetworkProtocol
{
    // --- Типы сообщений (Commands) ---
    public static class Commands
    {
        public const string Join = "JOIN";
        public const string Joined = "JOINED"; // Ответ сервера
        public const string MatchFound = "MATCH_FOUND";
        public const string PlaceShipsRandomly = "PLACE_SHIPS_RANDOMLY";
        public const string ShipPlacementConfirmed = "SHIP_PLACEMENT_CONFIRMED";
        public const string ShipPlaced = "SHIP_PLACED";
        public const string ShipPlacement = "SHIP_PLACEMENT";
        public const string AllShipsPlaced = "ALL_SHIPS_PLACED";
        public const string GameStart = "GAME_START"; // Начало игры
        public const string Attack = "ATTACK";
        public const string AttackResult = "ATTACK_RESULT";
        public const string YourTurn = "YOUR_TURN";
        public const string YourTurnAgain = "YOUR_TURN_AGAIN"; // Доп. ход
        public const string OpponentTurn = "OPPONENT_TURN";
        public const string GameOver = "GAME_OVER";
        public const string OpponentLeft = "OPPONENT_LEFT";
        public const string OpponentDisconnected = "OPPONENT_DISCONNECTED";
        public const string ChatMessage = "CHAT_MESSAGE";
        public const string ChatMessageReceived = "CHAT_MESSAGE_RECEIVED";
        public const string Error = "ERROR";
        public const string LeaveGame = "LEAVE_GAME";
    }

    // --- Ключи данных (Keys) ---
    public static class Keys
    {
        public const string Name = "name";
        public const string PlayerName = "player_name";
        public const string PlayerId = "player_id";
        public const string OpponentName = "opponent_name";
        public const string YourTurn = "your_turn";
        public const string X = "x";
        public const string Y = "y";
        public const string Hit = "hit";
        public const string Sunk = "sunk";
        public const string GameOver = "game_over";
        public const string Winner = "winner";
        public const string Message = "message";
        public const string AttackerId = "attacker_id";
        public const string Size = "size";
        public const string Horizontal = "horizontal";
        public const string ShipPlaced = "ships_placed";
        public const string SunkShipPositions = "sunk_ship_positions";
        public const string BlockedCells = "blocked_cells";
        public const string ChatText = "text";
        public const string Sender = "sender";
    }
}