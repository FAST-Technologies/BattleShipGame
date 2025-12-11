namespace BattleShipGame.Networking;

/// <summary>
/// Статический класс, определяющий протокол обмена сообщениями между клиентом и сервером.
/// </summary>
/// <remarks>
/// Протокол использует текстовый формат: "COMMAND:key1=value1;key2=value2".
/// Все сообщения заканчиваются символом новой строки (\n).
/// </remarks>
public static class NetworkProtocol
{
    /// <summary>
    /// Типы сообщений (команды) используемые в протоколе.
    /// </summary>
    public static class Commands
    {
        /// <summary>Запрос на присоединение к игре.</summary>
        public const string Join = "JOIN";
        
        /// <summary>Подтверждение присоединения от сервера.</summary>
        public const string Joined = "JOINED";
        
        /// <summary>Уведомление о найденном сопернике.</summary>
        public const string MatchFound = "MATCH_FOUND";
        
        /// <summary>Запрос на случайную расстановку кораблей.</summary>
        public const string PlaceShipsRandomly = "PLACE_SHIPS_RANDOMLY";
        
        /// <summary>Подтверждение расстановки кораблей.</summary>
        public const string ShipPlacementConfirmed = "SHIP_PLACEMENT_CONFIRMED";
        
        /// <summary>Уведомление о размещенном корабле.</summary>
        public const string ShipPlaced = "SHIP_PLACED";
        
        /// <summary>Информация о размещении корабля.</summary>
        public const string ShipPlacement = "SHIP_PLACEMENT";
        
        /// <summary>Все корабли расставлены.</summary>
        public const string AllShipsPlaced = "ALL_SHIPS_PLACED";
        
        /// <summary>Начало игровой сессии.</summary>
        public const string GameStart = "GAME_START";
        
        /// <summary>Атака по координатам.</summary>
        public const string Attack = "ATTACK";
        
        /// <summary>Результат атаки.</summary>
        public const string AttackResult = "ATTACK_RESULT";
        
        /// <summary>Уведомление о ходе игрока.</summary>
        public const string YourTurn = "YOUR_TURN";
        
        /// <summary>Дополнительный ход (при попадании).</summary>
        public const string YourTurnAgain = "YOUR_TURN_AGAIN";
        
        /// <summary>Ход противника.</summary>
        public const string OpponentTurn = "OPPONENT_TURN";
        
        /// <summary>Завершение игры.</summary>
        public const string GameOver = "GAME_OVER";
        
        /// <summary>Противник покинул игру.</summary>
        public const string OpponentLeft = "OPPONENT_LEFT";
        
        /// <summary>Разрыв соединения с противником.</summary>
        public const string OpponentDisconnected = "OPPONENT_DISCONNECTED";
        
        /// <summary>Отправка сообщения в чат.</summary>
        public const string ChatMessage = "CHAT_MESSAGE";
        
        /// <summary>Получение сообщения чата.</summary>
        public const string ChatMessageReceived = "CHAT_MESSAGE_RECEIVED";
        
        /// <summary>Сообщение об ошибке.</summary>
        public const string Error = "ERROR";
        
        /// <summary>Выход из игры.</summary>
        public const string LeaveGame = "LEAVE_GAME";
        
        /// <summary>Запрос списка игр.</summary>
        public const string ListReq = "LIST_REQ";
        
        /// <summary>Ответ со списком игр.</summary>
        public const string ListRes = "LIST_RES";
    }

    /// <summary>
    /// Ключи данных, используемые в сообщениях протокола.
    /// </summary>
    public static class Keys
    {
        /// <summary>Имя игрока.</summary>
        public const string Name = "name";
        
        /// <summary>Имя игрока (альтернативный ключ).</summary>
        public const string PlayerName = "player_name";
        
        /// <summary>Идентификатор игрока.</summary>
        public const string PlayerId = "player_id";
        
        /// <summary>Имя противника.</summary>
        public const string OpponentName = "opponent_name";
        
        /// <summary>Флаг хода игрока.</summary>
        public const string YourTurn = "your_turn";
        
        /// <summary>X-координата.</summary>
        public const string X = "x";
        
        /// <summary>Y-координата.</summary>
        public const string Y = "y";
        
        /// <summary>Флаг попадания при атаке.</summary>
        public const string Hit = "hit";
        
        /// <summary>Флаг потопления корабля.</summary>
        public const string Sunk = "sunk";
        
        /// <summary>Флаг завершения игры.</summary>
        public const string GameOver = "game_over";
        
        /// <summary>Имя победителя.</summary>
        public const string Winner = "winner";
        
        /// <summary>Текст сообщения (ошибки или чата).</summary>
        public const string Message = "message";
        
        /// <summary>Идентификатор атакующего.</summary>
        public const string AttackerId = "attacker_id";
        
        /// <summary>Размер корабля.</summary>
        public const string Size = "size";
        
        /// <summary>Ориентация корабля (true/false).</summary>
        public const string Horizontal = "horizontal";
        
        /// <summary>Количество размещенных кораблей.</summary>
        public const string ShipPlaced = "ships_placed";
        
        /// <summary>Позиции потопленного корабля.</summary>
        public const string SunkShipPositions = "sunk_ship_positions";
        
        /// <summary>Заблокированные клетки вокруг корабля.</summary>
        public const string BlockedCells = "blocked_cells";
        
        /// <summary>Текст чат-сообщения.</summary>
        public const string ChatText = "text";
        
        /// <summary>Отправитель сообщения.</summary>
        public const string Sender = "sender";
    }
}