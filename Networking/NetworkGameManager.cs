using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using BattleShipGame.Models2;

namespace BattleShipGame.Networking;

public class NetworkGameManager
{
    #region Поля и свойства
    
    private NetworkClient _networkClient; /// <summary>Инициализация сетевого клиента.</summary>
    private ChatManager? _chatManager; /// <summary>Инициализация чат-менеджера.</summary>
    
    private GameBoard? _playerBoard; /// <summary>Собственная игровая доска игрока.</summary>
    private GameBoard? _opponentBoard; /// <summary>Доска соперника в сетевой игре.</summary>
    
    private string _playerName = "Player"; /// <summary>Имя текущего игрока.</summary>
    private string _opponentName = "Opponent"; /// <summary>Имя соперника в сетевой игре.</summary>
    private string _myPlayerId = ""; /// <summary>ID игрока, присвоенный сервером.</summary>
    
    private bool _localShipsPlaced = false; /// <summary>true — свои корабли уже расставлены и отправлены серверу.</summary>
    private bool _opponentShipsPlaced = false; /// <summary>true — соперник завершил расстановку (получено от сервера).</summary>
    private bool _isProcessingNetworkAttack = false; /// <summary>Блокировка повторных атак пока ждём результат от сервера.</summary>
    
    private NetworkGameMode _networkMode = NetworkGameMode.None; /// <summary>Состояние сетевого подключения.</summary>
    
    // События - более полный набор
    public event Action<string>? StatusChanged;
    public event Action<bool>? PlayerTurnChanged;
    public event Action<string, string>? GameStarted;
    public event Action<string, bool>? GameOver;
    public event Action<string>? OpponentLeft;
    public event Action<string>? OpponentDisconnected;
    public event Action<string>? ConnectionLost;
    
    // События для детального обновления UI
    public event Action<int, int, bool, bool, bool, bool, Dictionary<string, string>>? AttackResultReceived;
    public event Action<string>? JoinedReceived;
    public event Action? MatchFoundReceived;
    public event Action<bool>? GameStartReceived;
    public event Action? YourTurnReceived;
    public event Action? YourTurnAgainReceived;
    public event Action? OpponentTurnReceived;
    
    /// <summary>
    /// Инициализирует сетевой менеджер на основе информации о сетевом клиенте.
    /// </summary>
    /// <param name="networkClient">Текущий сетевой клиент.</param>
    public NetworkGameManager(NetworkClient networkClient)
    {
        _networkClient = networkClient;
        //_chatManager = new ChatManager(networkClient, _playerName);
        _networkClient.OnMessageReceived += OnNetworkMessageReceived;
        _networkClient.OnDisconnected += OnNetworkDisconnected;
    }
    
    # endregion
    
    #region Public Methods
    
    // =====================================================================
    // Сетевое взаимодействие
    // =====================================================================
    
    /// <summary>
    /// Подключается к указанному серверу и отправляет запрос на вступление в игру.
    /// </summary>
    /// <param name="hostname">IP-адрес или домен сервера.</param>
    /// <param name="port">Порт сервера (по умолчанию 8889).</param>
    /// <param name="playerName">Игровое имя (никнейм).</param>
    /// <returns>Кортеж (успешно, сообщение для пользователя).</returns>
    public async Task<(bool success, string errorMessage)> ConnectToServer(string hostname, int port, string playerName)
    {
        _playerName = playerName;
        
        try
        {
            if (await _networkClient.ConnectAsync(hostname, port))
            {
                var joinMsg = new NetworkMessage
                {
                    Type = NetworkProtocol.Commands.Join, 
                    Data = { { NetworkProtocol.Keys.Name, _playerName } }
                };
                await _networkClient.SendMessageAsync(joinMsg);
                _networkMode = NetworkGameMode.Searching;
                return (true, "[Client] Подключение к серверу... Ищу соперника...");
            }
            else
            {
                return (false, "[Client] Не удалось подключиться к серверу.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Исключение при подключении: {ex.Message}");
            return (false, $"[ERROR] Ошибка подключения: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Уствановка чатового менеджера.
    /// </summary>
    /// <param name="chatManager">Текущий менеджер.</param>
    public void SetChatManager(ChatManager chatManager)
    {
        _chatManager = chatManager;
    }
    
    /// <summary>
    /// Отправка атаки.
    /// </summary>
    /// <param name="x">Координата клетки по x.</param>
    /// <param name="y">Координата клетки по y.</param>
    public async Task<bool> SendAttackAsync(int x, int y)
    {
        if (!_networkClient.IsConnected || _isProcessingNetworkAttack)
            return false;
        
        _isProcessingNetworkAttack = true;
        
        try
        {
            Console.WriteLine($"[Network] Sending ATTACK message: x={x}, y={y}");
            
            var attackMsg = new NetworkMessage 
            { 
                Type = NetworkProtocol.Commands.Attack, 
                Data = { 
                    { NetworkProtocol.Keys.X, x.ToString() }, 
                    { NetworkProtocol.Keys.Y, y.ToString() } 
                } 
            };
            await _networkClient.SendMessageAsync(attackMsg);
            
            StatusChanged?.Invoke("Атака отправлена... Ждем результата...");
            Console.WriteLine($"[Network] ATTACK message sent successfully");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network Error] Error sending attack: {ex.Message}");
            StatusChanged?.Invoke($"Ошибка отправки атаки: {ex.Message}");
            _isProcessingNetworkAttack = false;
            return false;
        }
    }
    
    /// <summary>
    /// Отправка информации о кораблях.
    /// </summary>
    /// <param name="playerBoard">Текущая игровая доска.</param>
    public async Task SendShipPlacementAsync(GameBoard playerBoard)
    {
        _playerBoard = playerBoard;
        _localShipsPlaced = true;
        
        // Отправляем расположение кораблей
        try
        {
            var shipData = new Dictionary<string, string>();
            for (int i = 0; i < playerBoard.Ships.Count; i++)
            {
                var ship = playerBoard.Ships[i];
                shipData[$"ship{i}_size"] = ship.Size.ToString();
                shipData[$"ship{i}_horizontal"] = ship.IsHorizontal.ToString();
                shipData[$"ship{i}_positions"] = string.Join(",", ship.Positions.Select(p => $"{p.X}:{p.Y}"));
            }
            
            var placementMsg = new NetworkMessage 
            { 
                Type = NetworkProtocol.Commands.ShipPlacement, 
                Data = shipData 
            };
            await _networkClient.SendMessageAsync(placementMsg);
            
            // Сообщаем, что расстановка завершена
            var readyMsg = new NetworkMessage { Type = NetworkProtocol.Commands.AllShipsPlaced };
            await _networkClient.SendMessageAsync(readyMsg);
            Console.WriteLine($"[Network] Отправлена информация о {playerBoard.Ships.Count} кораблях");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Ошибка отправки размещения кораблей: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Отправка сообщения в чат.
    /// </summary>
    public async Task SendChatMessageAsync(string text)
    {
        await _chatManager.SendChatMessageAsync(text);
    }
    
    /// <summary>
    /// Корректно выходит из сетевой игры.
    /// </summary>
    public async Task LeaveGameAsync()
    {
        Console.WriteLine("[DEBUG] Leaving network game...");
        if (_networkClient.IsConnected)
        {
            try
            {
                var leaveMsg = new NetworkMessage 
                { 
                    Type = NetworkProtocol.Commands.LeaveGame,
                    Data = { { NetworkProtocol.Keys.PlayerId, _myPlayerId } }
                };
                await _networkClient.SendMessageAsync(leaveMsg);
                Console.WriteLine("[Network] Отправлен запрос на выход из игры");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error sending LEAVE_GAME: {ex.Message}");
                Console.WriteLine($"[Network] Ошибка отправки запроса на выход: {ex.Message}");
            }
            _networkClient.Disconnect();
        }
        
        ResetState();
        Console.WriteLine("[DEBUG] Network game left");
    }
    
    /// <summary>
    /// Сброс состояния.
    /// </summary>
    public void ResetState()
    {
        _networkMode = NetworkGameMode.None;
        _myPlayerId = "";
        _localShipsPlaced = false;
        _opponentShipsPlaced = false;
        _isProcessingNetworkAttack = false;
        _chatManager.Clear();
    }
    
    /// <summary>
    /// Инициализация доски для новой сетевой игры.
    /// </summary>
    public void InitializeGameBoards()
    {
        _playerBoard = new GameBoard();
        _opponentBoard = new GameBoard();
        _localShipsPlaced = false;
        _opponentShipsPlaced = false;
        _isProcessingNetworkAttack = false;
    }
    
    #endregion
    
    #region Properties
    /// <summary>
    /// Свойства для перечисленных выше свойств.
    /// </summary>
    public NetworkGameMode NetworkMode => _networkMode;
    public ChatManager ChatManager => _chatManager;
    public bool IsConnected => _networkClient.IsConnected;
    public string PlayerName => _playerName;
    public string OpponentName => _opponentName;
    public string MyPlayerId => _myPlayerId;
    public bool IsProcessingAttack => _isProcessingNetworkAttack;
    public bool LocalShipsPlaced => _localShipsPlaced;
    public bool OpponentShipsPlaced => _opponentShipsPlaced;
    
    public GameBoard PlayerBoard 
    {
        get => _playerBoard;
        set => _playerBoard = value;
    }
    
    public GameBoard OpponentBoard 
    {
        get => _opponentBoard;
        set => _opponentBoard = value;
    }
    
    #endregion
    
    #region Private Methods
    
    // =====================================================================
    // Сетевое взаимодействие
    // =====================================================================
    
    /// <summary>
    /// Обработчик всех входящих сетевых сообщений. Распределяет их по соответствующим методам.
    /// </summary>
    /// <param name="message">Полученное сообщение от сервера.</param>
    private void OnNetworkMessageReceived(NetworkMessage message)
    {
        Console.WriteLine($"[Network] Получено: {message.Type} - {string.Join(", ", message.Data.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        
        // Делегируем обработку в UI поток
        Dispatcher.UIThread.Post(() => ProcessNetworkMessage(message));
    }
    
    private void ProcessNetworkMessage(NetworkMessage message)
    {
        switch (message.Type.ToUpper())
        {
            case NetworkProtocol.Commands.Joined:
                HandleJoinedMessage(message);
                break;
            case NetworkProtocol.Commands.MatchFound:
                HandleMatchFoundMessage(message);
                break;
            case NetworkProtocol.Commands.GameStart:
                HandleGameStartMessage(message);
                break;
            case NetworkProtocol.Commands.YourTurn:
                HandleYourTurnMessage();
                break;
            case NetworkProtocol.Commands.YourTurnAgain:
                HandleYourTurnAgainMessage();
                break;
            case NetworkProtocol.Commands.OpponentTurn:
                HandleOpponentTurnMessage();
                break;
            case NetworkProtocol.Commands.AttackResult:
                HandleAttackResultMessage(message);
                break;
            case NetworkProtocol.Commands.GameOver:
                HandleGameOverMessage(message);
                break;
            case NetworkProtocol.Commands.OpponentLeft:
                HandleOpponentLeftMessage(message);
                break;
            case NetworkProtocol.Commands.OpponentDisconnected:
                HandleOpponentDisconnectedMessage();
                break;
            case NetworkProtocol.Commands.ChatMessageReceived:
                HandleChatMessage(message.Data);
                break;
            case NetworkProtocol.Commands.Error:
                HandleErrorMessage(message);
                break;
            default:
                Console.WriteLine($"[Network] Неизвестная команда: {message.Type}");
                break;
        }
    }
    
    /// <summary>
    /// Обработка отключения от сервера.
    /// </summary>
    private void OnNetworkDisconnected()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            StatusChanged?.Invoke("[WARNING] Соединение с сервером потеряно.");
            ConnectionLost?.Invoke("Соединение с сервером потеряно.");
            ResetState();
        });
        Console.WriteLine("[Network] Отключен от сервера");
    }
    
    #endregion
    
    #region Message Handlers
    
    /// <summary>
    /// Обрабатывает сообщение о успешном подключении к серверу.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    private void HandleJoinedMessage(NetworkMessage message)
    {
        _playerName = message.Data.GetValueOrDefault(NetworkProtocol.Keys.PlayerName, _playerName);
        _myPlayerId = message.Data.GetValueOrDefault(NetworkProtocol.Keys.PlayerId, _myPlayerId);
        
        // Обновляем ChatManager с новым именем
        // _chatManager = new ChatManager(_networkClient, _playerName);
        
        JoinedReceived?.Invoke($"Подключено к серверу как {_playerName} (ID: {_myPlayerId}). Ищу соперника...");
        StatusChanged?.Invoke($"Подключено к серверу как {_playerName} (ID: {_myPlayerId}). Ищу соперника...");
    }
    
    /// <summary>
    /// Обрабатывает сообщение о найденном сопернике.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param> 
    private void HandleMatchFoundMessage(NetworkMessage message)
    {
        _networkMode = NetworkGameMode.InGame;
        _opponentName = message.Data.GetValueOrDefault(NetworkProtocol.Keys.OpponentName, "Unknown");
        
        // Инициализируем доски
        InitializeGameBoards();
        
        MatchFoundReceived?.Invoke();
        StatusChanged?.Invoke($"Найден соперник: {_opponentName}! Начинаем расстановку...");
        GameStarted?.Invoke(_playerName, _opponentName);
    }
    
    /// <summary>
    /// Обрабатывает сообщение о начале игры.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    private void HandleGameStartMessage(NetworkMessage message)
    {
        Console.WriteLine($"[DEBUG] GAME_START received, setting playerTurn to: {message.Data.GetValueOrDefault(NetworkProtocol.Keys.YourTurn, "false") == "true"}");
        
        bool playerTurn = message.Data.GetValueOrDefault(NetworkProtocol.Keys.YourTurn, "false") == "true";
        
        GameStartReceived?.Invoke(playerTurn);
        PlayerTurnChanged?.Invoke(playerTurn);
        StatusChanged?.Invoke(playerTurn ? "⚔️ Ваш ход! Атакуйте поле соперника!" : $"💭 Ход соперника ({_opponentName})...");
        
        Console.WriteLine($"[DEBUG] GAME_START processed, playerTurn is: {playerTurn}");
    }
    
    /// <summary>
    /// Обрабатывает сообщение о ходе игрока.
    /// </summary>
    private void HandleYourTurnMessage()
    {
        Console.WriteLine("[DEBUG] YOUR_TURN received");
        
        YourTurnReceived?.Invoke();
        PlayerTurnChanged?.Invoke(true);
        _isProcessingNetworkAttack = false;
        StatusChanged?.Invoke("⚔️ Ваш ход! Атакуйте поле соперника!");
    }
    
    /// <summary>
    /// Обрабатывает сообщение о повторном ходе игрока.
    /// </summary>
    private void HandleYourTurnAgainMessage()
    {
        Console.WriteLine("[DEBUG] YOUR_TURN_AGAIN received");
        
        YourTurnAgainReceived?.Invoke();
        PlayerTurnChanged?.Invoke(true);
        _isProcessingNetworkAttack = false;
        StatusChanged?.Invoke("🔥 Попадание! Стреляйте снова!");
    }
    
    /// <summary>
    /// Обрабатывает сообщение о ходе противника.
    /// </summary>
    private void HandleOpponentTurnMessage()
    {
        Console.WriteLine("[DEBUG] OPPONENT_TURN received");
        
        OpponentTurnReceived?.Invoke();
        PlayerTurnChanged?.Invoke(false);
        StatusChanged?.Invoke($"💭 Ход соперника ({_opponentName})...");
    }
    
    /// <summary>
    /// Обрабатывает сообщение о результате атаки.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    private void HandleAttackResultMessage(NetworkMessage message)
    {
        Console.WriteLine("[DEBUG] ATTACK_RESULT received");
        
        if (int.TryParse(message.Data.GetValueOrDefault(NetworkProtocol.Keys.X, ""), out int x) &&
            int.TryParse(message.Data.GetValueOrDefault(NetworkProtocol.Keys.Y, ""), out int y))
        {
            bool hit = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Hit, "false") == "true";
            bool sunk = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Sunk, "false") == "true";
            bool gameOver = message.Data.GetValueOrDefault(NetworkProtocol.Keys.GameOver, "false") == "true";
            var attackerId = message.Data.GetValueOrDefault(NetworkProtocol.Keys.AttackerId, "");
            bool isMyAttack = attackerId == _myPlayerId;
            
            Console.WriteLine($"[DEBUG] ATTACK_RESULT: ({x},{y}), hit={hit}, sunk={sunk}, gameOver={gameOver}, isMyAttack={isMyAttack}");
            
            // Снимаем блокировку атаки
            _isProcessingNetworkAttack = false;
            
            // Передаем все данные для обработки в MainWindow
            AttackResultReceived?.Invoke(x, y, hit, sunk, gameOver, isMyAttack, message.Data);
        }
        else
        {
            Console.WriteLine("[DEBUG] Failed to parse ATTACK_RESULT coordinates");
        }
    }
    
    /// <summary>
    /// Обрабатывает сообщение об окончании игры.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    private void HandleGameOverMessage(NetworkMessage message)
    {
        Console.WriteLine("[DEBUG] GAME_OVER received");
        
        string winnerName = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Winner, "Unknown");
        bool iWon = winnerName == _playerName;
        
        // Сохраняем состояние окончания игры
        _networkMode = NetworkGameMode.None;
        
        GameOver?.Invoke(winnerName, iWon);
    }
    
    /// <summary>
    /// Обрабатывает сообщение о выходе противника.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param> 
    private void HandleOpponentLeftMessage(NetworkMessage message)
    {
        Console.WriteLine("[DEBUG] OPPONENT_LEFT received");
        
        var leftMessage = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Message, "Соперник покинул игру");
        
        // Сбрасываем состояние
        ResetState();
        
        OpponentLeft?.Invoke(leftMessage);
    }
    
    /// <summary>
    /// Обрабатывает сообщение о дисконнекте противника.
    /// </summary>
    private void HandleOpponentDisconnectedMessage()
    {
        Console.WriteLine("[DEBUG] OPPONENT_DISCONNECTED received");
        
        // Сбрасываем состояние
        ResetState();
        
        OpponentDisconnected?.Invoke("Соперник отключился от игры");
    }
    
    /// <summary>
    /// Обрабатывает сообщение о сообщении в чате.
    /// </summary>
    /// <param name="data">Сообщение в чате.</param>
    private void HandleChatMessage(Dictionary<string, string> data)
    {
        _chatManager.HandleChatMessage(data);
    }
    
    /// <summary>
    /// Обрабатывает сообщение об ошибке.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    private void HandleErrorMessage(NetworkMessage message)
    {
        StatusChanged?.Invoke($"[ERROR] Ошибка сервера: {message.Data.GetValueOrDefault(NetworkProtocol.Keys.Message, "Неизвестная ошибка")}");
    }
    #endregion
}