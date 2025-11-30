using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BattleShipGame2.Models;
using BattleShipGame2.Networking;

namespace BattleShipGame2.ServerLogic;

public class GameServer
{
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<string, PlayerConnection> _players = new();
    private ConcurrentQueue<PlayerConnection> _waitingPlayers = new();
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private bool _isRunning = false;

    public GameServer(int port = 8889)
    {
        _listener = new TcpListener(IPAddress.Any, port);
    }

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

    public void Stop()
    {
        _isRunning = false;
        _listener.Stop();
        // Закрываем все соединения
        foreach (var player in _players.Values)
        {
            player.Close();
        }
        _players.Clear();
        _waitingPlayers.Clear();
        _rooms.Clear();
    }

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        PlayerConnection player = null;
        try
        {
            player = new PlayerConnection(tcpClient);
            _players[player.Id] = player;

            Console.WriteLine($"[SERVER] Клиент подключился: {player.Id}");

            string line;
            while ((line = await player.Reader.ReadLineAsync()) != null)
            {
                Console.WriteLine($"[SERVER] Получено от {player.Name ?? player.Id}: {line}");
                var msg = ParseMessage(line);
                if (msg != null)
                {
                    await ProcessMessageAsync(player, msg);
                }
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

    private async Task ProcessMessageAsync(PlayerConnection player, ClientMessage message)
    {
        switch (message.Type.ToUpper())
        {
            case NetworkProtocol.Commands.Join:
                await HandleJoinAsync(player, message.Data.GetValueOrDefault(NetworkProtocol.Keys.Name, "Anon"));
                break;
            case NetworkProtocol.Commands.PlaceShipsRandomly:
                // Обработка запроса на случайную расстановку (сервер может подтвердить)
                // В реальной игре сервер должен проверять и подтверждать действия
                // Здесь просто игнорируем, клиент сам решает
                break;
            case NetworkProtocol.Commands.ShipPlaced:
                // Клиент сообщает о размещении корабля
                // В реальной игре сервер проверяет корректность
                // Здесь просто игнорируем, клиент сам отслеживает
                break;
            case NetworkProtocol.Commands.ShipPlacement:
                // Клиент отправляет расположение всех кораблей
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
            // Добавьте другие типы сообщений по мере необходимости
            default:
                Console.WriteLine($"[SERVER] Неизвестная команда от {player.Name} ({player.Id}): {message.Type}");
                // Можно отправить ошибку
                await SendServerMessageAsync(player, new ServerMessage
                {
                    Type = NetworkProtocol.Commands.Error,
                    Data = { { NetworkProtocol.Keys.Message, $"[SERVER] Неизвестная команда: {message.Type}" } }
                });
                break;
        }
    }
    
    private async Task HandleChatMessageAsync(PlayerConnection sender, Dictionary<string, string> data)
    {
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == sender.Id || r.Player2.Id == sender.Id);
        if (room == null || !room.GameStarted) 
        {
            Console.WriteLine($"[SERVER] Чат от {sender.Name} отклонён: игра не началась или комната не найдена");
            return;
        }
    
        var recipient = room.Player1.Id == sender.Id ? room.Player2 : room.Player1;
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
    
    private async Task HandleList(string playerID)
    {
        Dictionary<string, string> message = new Dictionary<string, string>();
        foreach (string id in _players.Keys)
        {
            if (id == playerID) continue;
            message.Add(id, _players[playerID].Name);
        }
        await SendServerMessageAsync(_players[playerID], new ServerMessage
        {
            Type = NetworkProtocol.Commands.ListRes, 
            Data = message
        });
    }
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

        // Проверяем, есть ли пара
        if (_waitingPlayers.Count >= 2)
        {
            if (_waitingPlayers.TryDequeue(out var p1) && _waitingPlayers.TryDequeue(out var p2))
            {
                var room = new GameRoom(p1, p2);
                _rooms[room.Id] = room;

                p1.IsReady = false;
                p2.IsReady = false;
                p1.IsMyTurn = new Random().Next(2) == 0; // Рандомный первый ход
                p2.IsMyTurn = !p1.IsMyTurn;

                // Уведомляем игроков о начале игры
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
    
    private async Task HandleShipPlacementAsync(PlayerConnection player, Dictionary<string, string> data)
    {
        Console.WriteLine($"[SERVER] Получено размещение кораблей от {player.Name}");
        
        // Очищаем доску игрока на сервере
        player.Board.Clear();
        
        // Парсим и размещаем корабли
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
                    
                    // Размещаем корабль на доске сервера
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
        
        // Отправляем подтверждение (опционально)
        await SendServerMessageAsync(player, new ServerMessage 
        { 
            Type = NetworkProtocol.Commands.ShipPlacementConfirmed, 
            Data = { { NetworkProtocol.Keys.ShipPlaced, player.Board.Ships.Count.ToString() } } 
        });
    }
    
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

    private async Task HandleAttackAsync(PlayerConnection attacker, Dictionary<string, string> data)
    {
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == attacker.Id || r.Player2.Id == attacker.Id);
        if (room == null || !room.GameStarted || room.GameOver) 
        {
            Console.WriteLine($"[SERVER] Атака от {attacker.Name} отклонена: комната не найдена или игра не активна");
            return;
        }

        // Проверяем, ли это ход игрока
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
        
        var defender = attacker.Id == room.Player1.Id ? room.Player2 : room.Player1;
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

        // Базовый результат атаки
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
            var sunkShip = defenderBoard.Ships.FirstOrDefault(s => 
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
                            
                            if (checkX >= 0 && checkX < defenderBoard.Size && 
                                checkY >= 0 && checkY < defenderBoard.Size)
                            {
                                if (defenderBoard.Grid[checkX, checkY] == CellState.Blocked)
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
    
    private async Task HandleLeaveGameAsync(PlayerConnection player)
    {
        Console.WriteLine($"[SERVER] Игрок {player.Name} покидает игру");
    
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == player.Id || r.Player2.Id == player.Id);
        if (room != null)
        {
            var opponent = room.Player1.Id == player.Id ? room.Player2 : room.Player1;
        
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
    
    private async Task HandlePlayerDisconnect(PlayerConnection player)
    {
        var room = _rooms.Values.FirstOrDefault(r => r.Player1.Id == player.Id || r.Player2.Id == player.Id);
        if (room != null)
        {
            var opponent = room.Player1.Id == player.Id ? room.Player2 : room.Player1;
            await SendServerMessageAsync(opponent, new ServerMessage { Type = NetworkProtocol.Commands.OpponentDisconnected });
            _rooms.TryRemove(room.Id, out _);
        }
    }

    private void LeaveQueue(PlayerConnection player)
    {
        // Простой способ "удалить" из ConcurrentQueue - пройти и перезаписать
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

    private void RemovePlayer(PlayerConnection player)
    {
        _players.TryRemove(player.Id, out _);
    }

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
}