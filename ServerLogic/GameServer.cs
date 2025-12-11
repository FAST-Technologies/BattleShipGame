using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BattleShipGame.Models2;
using BattleShipGame.Networking;

namespace BattleShipGame.ServerLogic;

/// <summary>
/// Сервер для игры Морской Бой.
/// </summary>
/// <remarks>
/// Обрабатывает подключения клиентов, создает игровые комнаты, управляет игровым процессом.
/// Использует асинхронные операции для поддержки множества одновременных подключений.
/// </remarks>
public class GameServer
{
    #region Поля и свойства
    private readonly TcpListener _listener; /// <summary>TCP клиент.</summary>
    private readonly ConcurrentDictionary<string, PlayerConnection> _players = new(); /// <summary>Игроки на сервере.</summary>
    private ConcurrentQueue<PlayerConnection> _waitingPlayers = new(); /// <summary>Игроки, ожидающие серверную игру.</summary>
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new(); /// <summary>Серверные комнаты.</summary>
    private bool _isRunning = false; /// <summary>Флаг для проверки работоспособности сервера.</summary>

    /// <summary>
    /// Создает новый экземпляр игрового сервера.
    /// </summary>
    /// <param name="port">Порт для прослушивания подключений.</param>
    /// <remarks>По умолчанию использует 8889 порт для подключения.</remarks>
    public GameServer(int port = 8889)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }
    
    #endregion

    #region Основные серверные методы
    /// <summary>
    /// Запускает сервер и начинает прослушивание подключений.
    /// </summary>
    /// <returns>Задача, представляющая асинхронную операцию запуска.</returns>
    public async Task StartAsync()
    {
        _listener.Start();
        _isRunning = true;
        Console.WriteLine($"[SERVER] Сервер морского боя запущен на порту { _listener.LocalEndpoint }...");

        while (_isRunning)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(client)); // Обработка клиента в отдельной задаче
            }
            catch (SocketException) when (!_isRunning)
            {
                // Сервер остановлен, выходим из цикла
                break;
            }
        }
    }

    /// <summary>
    /// Останавливает сервер и освобождает все ресурсы.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _listener.Stop();
        foreach (var player in _players.Values)
        {
            player.Close();
        }
        _players.Clear();
        _waitingPlayers.Clear();
        _rooms.Clear();
        Console.WriteLine("[SERVER] Сервер остановлен");
    }

    /// <summary>
    /// Обрабатывает подключение клиента.
    /// </summary>
    /// <param name="tcpClient">TCP-клиент подключения.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        PlayerConnection player = null;
        try
        {
            player = new PlayerConnection(tcpClient);
            _players[player.Id] = player;
            Console.WriteLine($"[SERVER] Клиент подключился: {player.Id}");
            string? line;
            while ((line = await player.Reader.ReadLineAsync()) != null)
            {
                Console.WriteLine($"[SERVER] Получено от {player.Name ?? player.Id}: {line}");
                var msg = ParseMessage(line);
                if (msg != null)
                    await ProcessMessageAsync(player, msg);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Ошибка обработки клиента {player?.Id}: {ex.Message}");
        }
        finally
        {
            if (player != null)
            {
                LeaveQueue(player);
                await HandlePlayerDisconnect(player);
                RemovePlayer(player);
                player.Close();
                Console.WriteLine($"[SERVER] Клиент отключился: {player.Id}");
            }
        }
    }

    /// <summary>
    /// Парсит строку сообщения в объект ClientMessage.
    /// </summary>
    /// <param name="line">Строка сообщения.</param>
    /// <returns>Объект ClientMessage или null при ошибке парсинга.</returns>
    private ClientMessage? ParseMessage(string line)
    {
        var parts = line.Split(':', 2);
        if (parts.Length < 2) return null;
        var message = new ClientMessage { Type = parts[0] };
        var dataPart = parts[1];
        if (!string.IsNullOrEmpty(dataPart))
        {
            var dataPairs = dataPart.Split(';');
            foreach (var pair in dataPairs)
            {
                var keyValue = pair.Split('=', 2);
                if (keyValue.Length == 2)
                {
                    message.Data[keyValue[0]] = keyValue[1];
                }
            }
        }
        return message;
    }

    /// <summary>
    /// Обрабатывает входящее сообщение от клиента.
    /// </summary>
    /// <param name="player">Игрок, отправивший сообщение.</param>
    /// <param name="message">Сообщение для обработки.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task ProcessMessageAsync(PlayerConnection player, ClientMessage message)
    {
        switch (message.Type.ToUpper())
        {
            case NetworkProtocol.Commands.Join:
                await HandleJoinAsync(player, message.Data.GetValueOrDefault(NetworkProtocol.Keys.Name, "Anon"));
                break;
            case NetworkProtocol.Commands.PlaceShipsRandomly:
            case NetworkProtocol.Commands.ShipPlaced:
                break;
            case NetworkProtocol.Commands.ShipPlacement:
                await HandleShipPlacementAsync(player, message.Data);
                break;
            case NetworkProtocol.Commands.AllShipsPlaced:
                await HandleShipsPlacedAsync(player);
                break;
            case NetworkProtocol.Commands.Attack:
                await HandleAttackAsync(player, message.Data);
                break;
            case NetworkProtocol.Commands.LeaveGame:
                await HandleLeaveGameAsync(player);
                break;
            case NetworkProtocol.Commands.ChatMessage:
                await HandleChatMessageAsync(player, message.Data);
                break;
            case NetworkProtocol.Commands.ListReq:
                await HandleList(player.Id);
                break;
            default:
                Console.WriteLine($"[SERVER] Неизвестная команда от {player.Name} ({player.Id}): {message.Type}");
                await SendServerMessageAsync(player, new ServerMessage
                {
                    Type = NetworkProtocol.Commands.Error,
                    Data = { { NetworkProtocol.Keys.Message, $"[SERVER] Неизвестная команда: {message.Type}" } }
                });
                break;
        }
    }
    
    /// <summary>
    /// Удаляет игрока из очереди ожидания.
    /// </summary>
    /// <param name="player">Игрок, которого необходимо удалить из очереди ожидания.</param>
    private void LeaveQueue(PlayerConnection player)
    {
        var remaining = new ConcurrentQueue<PlayerConnection>();
        PlayerConnection current;
        while (_waitingPlayers.TryDequeue(out current))
        {
            if (current.Id != player.Id)
            {
                remaining.Enqueue(current);
            }
        }
        _waitingPlayers = remaining;
    }

    /// <summary>
    /// Удаляет игрока из списка подключенных игроков.
    /// </summary>
    /// <param name="player">Игрок, которого необходимо удалить из списка подключенных игроков.</param>
    private void RemovePlayer(PlayerConnection player)
    {
        _players.TryRemove(player.Id, out _);
    }

    /// <summary>
    /// Отправляет сообщение игроку.
    /// </summary>
    /// <param name="player">Текущее игровое соединение игрока.</param>
    /// <param name="message">Сообщение, отправляемое игроку.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task SendServerMessageAsync(PlayerConnection player, ServerMessage message)
    {
        try
        {
            await player.Writer.WriteLineAsync(message.ToString());
            Console.WriteLine($"[SERVER] Отправлено {player.Name}: {message.Type}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SERVER] Ошибка отправки сообщения игроку {player.Id}: {ex.Message}");
            RemovePlayer(player);
            player.Close();
        }
    }
    
    /// <summary>
    /// Освобождает ресурсы сервера.
    /// </summary>
    public void Dispose()
    {
        Stop();
        _listener?.Stop();
    }
    
    #endregion
    
    #region Server Event Handlers
    
    /// <summary>
    /// Обрабатывает запрос на присоединение к игре.
    /// </summary>
    /// <param name="player">Игрок, запрашивающий присоединение.</param>
    /// <param name="name">Имя игрока.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleJoinAsync(PlayerConnection player, string name)
    {
        player.Name = name;
        // Отправляем подтверждение
        await SendServerMessageAsync(player, new ServerMessage
        {
            Type = NetworkProtocol.Commands.Joined, 
            Data =
            {
                { NetworkProtocol.Keys.PlayerName, name }, 
                { NetworkProtocol.Keys.PlayerId, player.Id }
            }
        });

        // Добавляем в очередь ожидания
        _waitingPlayers.Enqueue(player);
        Console.WriteLine($"[SERVER] Игрок {name} ({player.Id}) добавлен в очередь ожидания. Ожидающих: {_waitingPlayers.Count}");

        // Проверяем, есть ли пара для начала игры
        await TryCreateGameRoomAsync();
    }
    
    /// <summary>
    /// Пытается создать игровую комнату из ожидающих игроков.
    /// </summary>
    /// <returns>Задача, представляющая асинхронную операцию.</returns>
    private async Task TryCreateGameRoomAsync()
    {
        if (_waitingPlayers.Count >= 2)
        {
            if (_waitingPlayers.TryDequeue(out var p1) && _waitingPlayers.TryDequeue(out var p2))
            {
                var room = new GameRoom(p1, p2);
                _rooms[room.Id] = room;

                // Сбрасываем флаги готовности
                p1.IsReady = false;
                p2.IsReady = false;

                // Случайным образом определяем первого игрока
                var random = new Random();
                p1.IsMyTurn = random.Next(2) == 0;
                p2.IsMyTurn = !p1.IsMyTurn;

                // Уведомляем игроков о найденном сопернике
                await SendServerMessageAsync(p1, new ServerMessage
                {
                    Type = NetworkProtocol.Commands.MatchFound,
                    Data = { { NetworkProtocol.Keys.OpponentName, p2.Name } }
                });

                await SendServerMessageAsync(p2, new ServerMessage
                {
                    Type = NetworkProtocol.Commands.MatchFound,
                    Data = { { NetworkProtocol.Keys.OpponentName, p1.Name } }
                });

                Console.WriteLine($"[SERVER] Создана комната {room.Id} между {p1.Name} и {p2.Name}. Первый ход: {(p1.IsMyTurn ? p1.Name : p2.Name)}");
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает размещение кораблей игроком.
    /// </summary>
    /// <param name="player">Игрок, размещающий корабли.</param>
    /// <param name="data">Данные о размещении кораблей.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleShipPlacementAsync(PlayerConnection player, Dictionary<string, string> data)
    {
        Console.WriteLine($"[SERVER] Получено размещение кораблей от {player.Name}");
        player.Board.Clear();
        int shipIndex = 0;
        while (data.ContainsKey($"ship{shipIndex}_size"))
        {
            try
            {
                int size = int.Parse(data[$"ship{shipIndex}_size"]);
                bool isHorizontal = bool.Parse(data[$"ship{shipIndex}_horizontal"]);
                string positions = data[$"ship{shipIndex}_positions"];
                
                // Парсим позиции: "0:0,1:0,2:0,3:0"
                var positionPairs = positions.Split(',');
                if (positionPairs.Length > 0)
                {
                    var firstPos = positionPairs[0].Split(':');
                    int startX = int.Parse(firstPos[0]);
                    int startY = int.Parse(firstPos[1]);
                    
                    // Создаем корабль
                    var ship = new Ship(size, isHorizontal);
                    if (player.Board.PlaceShip(ship, startX, startY))
                    {
                        Console.WriteLine($"[SERVER] Корабль {shipIndex} размещен: размер={size}, horizontal={isHorizontal}, позиция=({startX},{startY})");
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER] ОШИБКА: Не удалось разместить корабль {shipIndex} на ({startX},{startY})");
                    }
                }
                shipIndex++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SERVER] Ошибка парсинга корабля {shipIndex}: {ex.Message}");
                break;
            }
        }
        
        Console.WriteLine($"[SERVER] Размещено {player.Board.Ships.Count} кораблей для {player.Name}");
        
        // Отправляем подтверждение размещения
        await SendServerMessageAsync(player, new ServerMessage 
        { 
            Type = NetworkProtocol.Commands.ShipPlacementConfirmed, 
            Data = { { NetworkProtocol.Keys.ShipPlaced, player.Board.Ships.Count.ToString() } } 
        });
    }
    
    /// <summary>
    /// Обрабатывает уведомление о завершении расстановки кораблей.
    /// </summary>
    /// <param name="player">Игрок, завершивший расстановку.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleShipsPlacedAsync(PlayerConnection player)
    {
        player.IsReady = true;
        Console.WriteLine($"[SERVER] Игрок {player.Name} ({player.Id}) готов (корабли расставлены).");

        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == player.Id || r.Player2.Id == player.Id);
        if (room != null && room.Player1.IsReady && room.Player2.IsReady)
        {
            room.GameStarted = true;
            Console.WriteLine($"[SERVER] Игра в комнате {room.Id} начинается!");
            // Уведомляем обоих игроков о начале игры и чей ход
            await SendServerMessageAsync(room.Player1, new ServerMessage 
            { 
                Type = NetworkProtocol.Commands.GameStart, 
                Data = { { NetworkProtocol.Keys.YourTurn, room.Player1.IsMyTurn.ToString().ToLower() } } 
            });
            await SendServerMessageAsync(room.Player2, new ServerMessage 
            { 
                Type = NetworkProtocol.Commands.GameStart, 
                Data = { { NetworkProtocol.Keys.YourTurn, room.Player2.IsMyTurn.ToString().ToLower() } } 
            });
        }
    }
    
    /// <summary>
    /// Обрабатывает атаку игрока.
    /// </summary>
    /// <param name="attacker">Игрок, выполняющий атаку.</param>
    /// <param name="data">Данные атаки (координаты).</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleAttackAsync(PlayerConnection attacker, Dictionary<string, string> data)
    {
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == attacker.Id || r.Player2.Id == attacker.Id);
        if (room == null || !room.GameStarted || room.GameOver) 
        {
            Console.WriteLine($"[SERVER] Атака от {attacker.Name} отклонена: комната не найдена или игра не активна");
            return;
        }
        
        if ((room.Player1.Id == attacker.Id && !room.Player1.IsMyTurn) ||
            (room.Player2.Id == attacker.Id && !room.Player2.IsMyTurn))
        {
            await SendServerMessageAsync(attacker, new ServerMessage 
            { 
                Type = NetworkProtocol.Commands.Error, 
                Data = { { NetworkProtocol.Keys.Message, "Not your turn" } } 
            });
            Console.WriteLine($"[SERVER] Атака от {attacker.Name} отклонена: не его ход");
            return;
        }

        if (!int.TryParse(data.GetValueOrDefault("x", ""), out int x) ||
            !int.TryParse(data.GetValueOrDefault("y", ""), out int y))
        {
            await SendServerMessageAsync(attacker, new ServerMessage 
            { 
                Type = NetworkProtocol.Commands.Error, 
                Data = { { NetworkProtocol.Keys.Message, "Invalid coordinates" } } 
            });
            return;
        }
        
        var defender = room.GetOpponent(attacker);
        if (defender == null)
        {
            Console.WriteLine($"[SERVER] Ошибка: не найден противник для {attacker.Name}");
            return;
        }
        var defenderBoard = defender.Board;

        // Проверяем, можно ли атаковать эту клетку
        var cellState = defenderBoard.Grid[x, y];
        if (cellState == CellState.Hit || cellState == CellState.Miss || 
            cellState == CellState.Sunk || cellState == CellState.Blocked)
        {
            await SendServerMessageAsync(attacker, new ServerMessage 
            { 
                Type = NetworkProtocol.Commands.Error, 
                Data = { { NetworkProtocol.Keys.Message, "Cell already attacked" } } 
            });
            Console.WriteLine($"[SERVER] Атака от {attacker.Name} по ({x},{y}) отклонена: клетка уже атакована (состояние={cellState})");
            return;
        }
    
        Console.WriteLine($"[SERVER] Атака от {attacker.Name} по ({x},{y}), состояние клетки до атаки: {cellState}");
    
        // Выполняем атаку
        var (hit, sunk, gameOver) = defenderBoard.Attack(x, y);

        Console.WriteLine($"[SERVER] Результат атаки: попадание={hit}, потоплен={sunk}, конец игры={gameOver}");

        // Отправляем результаты атаки обоим игрокам
        await SendAttackResultAsync(attacker, defender, x, y, hit, sunk, gameOver, defenderBoard);

        // Обрабатываем переход хода или завершение игры
        await HandleTurnTransitionAsync(room, attacker, defender, hit, gameOver);
    }
    
    /// <summary>
    /// Отправляет результаты атаки обоим игрокам.
    /// </summary>
    /// <param name="attacker">Атакающий игрок.</param>
    /// <param name="defender">Защищающийся игрок.</param>
    /// <param name="board">Игровое поле.</param>
    /// <param name="x">Координата клетки по x.</param>
    /// <param name="y">Координата клетки по y.</param>
    /// <param name="hit">Флаг попадения по кораблю.</param>
    /// <param name="sunk">Флаг потопления корабля.</param>
    /// <param name="gameOver">Флаг окончания игры.</param> 
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task SendAttackResultAsync(PlayerConnection attacker, PlayerConnection defender, int x, int y, bool hit, bool sunk, bool gameOver, GameBoard board)
    {
        var resultData = new Dictionary<string, string>
        {
            { NetworkProtocol.Keys.X, x.ToString() },
            { NetworkProtocol.Keys.Y, y.ToString() },
            { NetworkProtocol.Keys.Hit, hit.ToString().ToLower() },
            { NetworkProtocol.Keys.Sunk, sunk.ToString().ToLower() },
            { NetworkProtocol.Keys.GameOver, gameOver.ToString().ToLower() },
            { NetworkProtocol.Keys.AttackerId, attacker.Id }
        };
        
        // Если корабль потоплен, отправляем информацию о заблокированных клетках
        if (sunk)
        {
            var blockedCells = new List<string>();
            
            // Находим потопленный корабль
            var sunkShip = board.Ships.FirstOrDefault(s => 
                s.IsSunk && s.Positions.Any(p => p.X == x && p.Y == y));
            
            if (sunkShip != null)
            {
                Console.WriteLine($"[SERVER] Корабль потоплен! Позиции корабля: {string.Join(", ", sunkShip.Positions.Select(p => $"({p.X},{p.Y})"))}");
                
                // Собираем все заблокированные клетки вокруг корабля
                foreach (var pos in sunkShip.Positions)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int checkX = pos.X + dx;
                            int checkY = pos.Y + dy;
                            
                            if (checkX >= 0 && checkX < board.Size && 
                                checkY >= 0 && checkY < board.Size)
                            {
                                if (board.Grid[checkX, checkY] == CellState.Blocked)
                                {
                                    blockedCells.Add($"{checkX}:{checkY}");
                                }
                            }
                        }
                    }
                }
                
                // Добавляем позиции потопленного корабля
                resultData[NetworkProtocol.Keys.SunkShipPositions] = string.Join(",", 
                    sunkShip.Positions.Select(p => $"{p.X}:{p.Y}"));
                
                Console.WriteLine($"[SERVER] Заблокировано клеток: {blockedCells.Count}");
            }
            
            if (blockedCells.Count > 0)
            {
                resultData[NetworkProtocol.Keys.BlockedCells] = string.Join(",", blockedCells);
            }
        }

        var resultMsg = new ServerMessage
        {
            Type = NetworkProtocol.Commands.AttackResult,
            Data = resultData
        };

        await SendServerMessageAsync(attacker, resultMsg);
        await SendServerMessageAsync(defender, resultMsg);
    }

    /// <summary>
    /// Обрабатывает переход хода между игроками.
    /// </summary>
    /// <param name="room">Определённая комната на сервере.</param>
    /// <param name="attacker">Атакающий игрок.</param>
    /// <param name="defender">Защищающийся игрок.</param>
    /// <param name="hit">Флаг попадения по кораблю.</param>
    /// <param name="gameOver">Флаг окончания игры.</param> 
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleTurnTransitionAsync(GameRoom room, PlayerConnection attacker, PlayerConnection defender, bool hit, bool gameOver)
    {
        if (gameOver)
        {
            room.GameOver = true;
            var winMsg = new ServerMessage
            {
                Type = NetworkProtocol.Commands.GameOver,
                Data = { { NetworkProtocol.Keys.Winner, attacker.Name } }
            };
            await SendServerMessageAsync(attacker, winMsg);
            await SendServerMessageAsync(defender, winMsg);
            Console.WriteLine($"[SERVER] Игра в комнате {room.Id} завершена. Победитель: {attacker.Name}");
            _rooms.TryRemove(room.Id, out _);
        }
        else if (!hit) // Промах - ход переходит
        {
            attacker.IsMyTurn = false;
            defender.IsMyTurn = true;
            await SendServerMessageAsync(defender, new ServerMessage { Type = NetworkProtocol.Commands.YourTurn });
            await SendServerMessageAsync(attacker, new ServerMessage { Type = NetworkProtocol.Commands.OpponentTurn });
            Console.WriteLine($"[SERVER] Ход переходит к {defender.Name}");
        }
        else // Попадание - ход остается
        {
            await SendServerMessageAsync(attacker, new ServerMessage { Type = NetworkProtocol.Commands.YourTurnAgain });
            Console.WriteLine($"[SERVER] Попадание! {attacker.Name} стреляет снова");
        }
    }
    
    /// <summary>
    /// Обрабатывает сообщение чата от игрока.
    /// </summary>
    /// <param name="sender">Отправитель сообщения.</param>
    /// <param name="data">Сообщение с некоторой информацией.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleChatMessageAsync(PlayerConnection sender, Dictionary<string, string> data)
    {
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == sender.Id || r.Player2.Id == sender.Id);
        if (room == null || !room.GameStarted) 
        {
            Console.WriteLine($"[SERVER] Чат от {sender.Name} отклонён: игра не началась или комната не найдена");
            return;
        }
    
        var recipient = room.GetOpponent(sender);
        if (recipient == null)
        {
            Console.WriteLine($"[SERVER] Ошибка чата: не найден получатель для {sender.Name}");
            return;
        }
        var chatText = data.GetValueOrDefault(NetworkProtocol.Keys.ChatText, "");
    
        Console.WriteLine($"[SERVER] Чат от {sender.Name} к {recipient.Name}: {chatText}");
    
        // Пересылаем сообщение сопернику
        await SendServerMessageAsync(recipient, new ServerMessage
        {
            Type = NetworkProtocol.Commands.ChatMessageReceived,
            Data = 
            {
                { NetworkProtocol.Keys.Sender, sender.Name },
                { NetworkProtocol.Keys.ChatText, chatText }
            }
        });
    }
    
    /// <summary>
    /// Обрабатывает запрос списка игроков.
    /// </summary>
    /// <param name="playerId">Уникальный идентификатор игрока.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleList(string playerId)
    {
        if (!_players.TryGetValue(playerId, out var player))
            return;
        Dictionary<string, string> message = new Dictionary<string, string>();
        foreach (string id in _players.Keys)
        {
            if (id == playerId) continue;
            message.Add(id, _players[playerId].Name);
        }
        await SendServerMessageAsync(_players[playerId], new ServerMessage
        {
            Type = NetworkProtocol.Commands.ListRes, 
            Data = message
        });
    }
    
    /// <summary>
    /// Обрабатывает выход игрока из игры.
    /// </summary>
    /// <param name="player">Текущее игровое соединение игрока.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandleLeaveGameAsync(PlayerConnection player)
    {
        Console.WriteLine($"[SERVER] Игрок {player.Name} покидает игру");
    
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == player.Id || r.Player2.Id == player.Id);
        if (room != null)
        {
            var opponent = room.GetOpponent(player);
            if (opponent != null)
                // Уведомляем соперника
                await SendServerMessageAsync(opponent, new ServerMessage
                {
                    Type = NetworkProtocol.Commands.OpponentLeft,
                    Data = { { NetworkProtocol.Keys.Message, $"{player.Name} покинул игру" } }
                });
        
            // Удаляем комнату
            _rooms.TryRemove(room.Id, out _);
            Console.WriteLine($"[SERVER] Комната {room.Id} удалена");
        }
    }
    
    /// <summary>
    /// Обрабатывает отключение игрока.
    /// </summary>
    /// <param name="player">Текущее игровое соединение игрока.</param>
    /// <returns>Задача, представляющая асинхронную обработку.</returns>
    private async Task HandlePlayerDisconnect(PlayerConnection player)
    {
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == player.Id || r.Player2.Id == player.Id);
        var opponent = room?.GetOpponent(player);
        if (opponent != null)
            await SendServerMessageAsync(opponent, new ServerMessage { Type = NetworkProtocol.Commands.OpponentDisconnected });
        _rooms.TryRemove(room.Id, out _);
    }
    
    #endregion
}