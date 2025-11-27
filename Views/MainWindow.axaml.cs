using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using BattleShipGame2.Models;
using BattleShipGame2.Networking;

namespace BattleShipGame2.Views;

public partial class MainWindow : Window
{
    private GameBoard playerBoard; /// Собственная игровая доска.
    private GameBoard computerBoard; /// Игровая доска компьютера (Режим игры против компьютера).
    private GameBoard opponentBoard; /// Игровая доска соперника (Режим сетевой игры).
    private TextBlock statusText; /// Игровой статус.
    private TextBlock playerStatsText; /// Собственная статистика игрока.
    private TextBlock computerStatsText; /// Статистика компьютера (Режим игры против компьютера).
    private TextBlock opponentStatsText; /// Статистика противника (Режим сетевой игры).
    private GameMode currentMode = GameMode.Menu; /// Текущий игровой режим.
    private NetworkGameMode networkMode = NetworkGameMode.None; /// Текущее состояние сетевого подключения.
    private bool playerTurn = true; /// Флаг проверки хода для сетевой игры.
    private bool isPlayer2Turn = false; /// Флаг проверки хода для локальной игры.
    private Random random = new Random(); /// Инициализация рандомайзера.
    private int playerHits = 0; /// Количество собственных попадений игрока.
    private int playerMisses = 0; /// Количество собственных промахов игрока.
    private int computerHits = 0; /// Количество попадений компьютера.
    private int computerMisses = 0; /// Количество промахов компьютера.
    private int opponentHits = 0; /// Количество попадений оппонента.
    private int opponentMisses = 0; /// Количество промахов оппонента.
    private GameMode _lastGameMode = GameMode.VsComputer;

    // Для ручной расстановки
    private List<int> shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 }; /// Список размещаемых кораблей.
    private int currentShipIndex = 0; /// Изначальный индекс корабля.
    private bool currentShipHorizontal = true; /// Флаг положения корабля.
    private GameBoard placingBoard; /// Доска для ручного размещения кораблей.
    private bool placingPlayer1Ships = true; /// Флаг размещения кораблей первым игроком.

    private BotDifficulty botDifficulty = BotDifficulty.Easy; /// Сложность бота в режиме компьютера.
    private List<(int x, int y)> lastHits = new(); /// Массив последних попаданий.
    private (int x, int y)? lastHitDirection = null; /// Направление, в котором идём.
    private (int x, int y)? initialHit = null; /// Первое попадание в корабль.

    private Canvas placementCanvas; /// Поле для размещения кораблей.
    private Canvas ownCanvas;      /// Всегда левое поле (своё, с кораблями).
    private Canvas enemyCanvas;    /// Всегда правое поле (вражеское).
    
    // --- Сетевые поля ---
    private NetworkClient networkClient = new NetworkClient(); /// Инициализация сетевого клиента.
    private string playerName = "Player"; /// Имя клиента.
    private string opponentName = "Opponent"; /// Имя соперника.
    private bool localShipsPlaced = false; /// Для сетевой игры - размещены ли мои корабли.
    private bool opponentShipsPlaced = false; /// Для сетевой игры - размещены ли корабли соперника.
    private bool _isProcessingNetworkAttack = false; /// Флаг проверки атаки на корабль.
    private List<(string sender, string message)> chatMessages = new();
    private TextBox chatInputBox;
    private ScrollViewer chatScrollViewer;

    /// <summary>
    /// Открывает основное окно-меню.
    /// </summary>
    public MainWindow()
    {
        Title = "⚓️ Морской бой";
        Width = 1000;
        Height = 700;
        Background = new ImageBrush
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://BattleShipGame2/Assets/ShipWar.jpg"))),
            Stretch = Stretch.UniformToFill,
            Opacity = 0.6
        };
        // Подписка на сетевые события
        networkClient.OnMessageReceived += (msg) => 
            Dispatcher.UIThread.Post(() => OnNetworkMessageReceived(msg));
        networkClient.OnDisconnected += () => 
            Dispatcher.UIThread.Post(() => OnNetworkDisconnected());
        ShowMainMenu();
    }
    
    /// <summary>
    /// Подключается к серверу и отправляет запрос на присоединение.
    /// </summary>
    private async Task<(bool success, string errorMessage)> ConnectToServer(string hostname, int port)
    {
        try
        {
            if (await networkClient.ConnectAsync(hostname, port))
            {
                var joinMsg = new NetworkMessage
                {
                    Type = NetworkProtocol.Commands.Join, 
                    Data = { { NetworkProtocol.Keys.Name, playerName } }
                };
                await networkClient.SendMessageAsync(joinMsg);
                networkMode = NetworkGameMode.Searching;
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
    /// Обработчик входящих сетевых сообщений.
    /// </summary>
    private void OnNetworkMessageReceived(NetworkMessage message)
    {
        // Важно обновлять UI в основном потоке
        // Dispatcher.UIThread.Invoke(() =>
        // {
            Console.WriteLine($"[Network] Получено: {message.Type} - {string.Join(", ", message.Data.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            switch (message.Type.ToUpper())
            {
                case NetworkProtocol.Commands.Joined:
                    // Подтверждение подключения к серверу
                    playerName = message.Data.GetValueOrDefault(NetworkProtocol.Keys.PlayerName, playerName);
                    myPlayerId = message.Data.GetValueOrDefault(NetworkProtocol.Keys.PlayerId, myPlayerId);
                    if (statusText != null) statusText.Text = $"Подключено к серверу как {playerName}. Ищу соперника..."; // Проверка
                    break;
                case NetworkProtocol.Commands.MatchFound:
                    // Найден соперник, начинаем сетевую игру
                    networkMode = NetworkGameMode.InGame;
                    opponentName = message.Data.GetValueOrDefault(NetworkProtocol.Keys.OpponentName, "Unknown");
                    if (statusText != null) statusText.Text = $"Найден соперник: {opponentName}! Начинаем расстановку...";
                    StartNetworkGame();
                    break;
                case NetworkProtocol.Commands.GameStart:
                    Console.WriteLine($"[DEBUG] GAME_START received, setting playerTurn to: {message.Data.GetValueOrDefault(NetworkProtocol.Keys.YourTurn, "false") == "true"}");
                    playerTurn = message.Data.GetValueOrDefault(NetworkProtocol.Keys.YourTurn, "false") == "true";
                    if (statusText != null) statusText.Text = playerTurn ? "Ваш ход! Атакуйте поле соперника!" : $"Ход соперника ({opponentName})...";
                    Console.WriteLine($"[DEBUG] About to call ShowGameScreen, playerTurn is: {playerTurn}");
                    ShowGameScreen();
                    break;
                case NetworkProtocol.Commands.YourTurn:
                    Console.WriteLine("[DEBUG] YOUR_TURN received");
                    playerTurn = true;
                    if (statusText != null) statusText.Text = "⚔️ Ваш ход! Атакуйте поле соперника!";
                    // Перерисовываем доски, чтобы кнопки стали кликабельными
                    UpdateStatusAndBoards();
                    break;
                case NetworkProtocol.Commands.YourTurnAgain:
                    Console.WriteLine("[DEBUG] YOUR_TURN_AGAIN received");
                    playerTurn = true; // Остается true
                    _isProcessingNetworkAttack = false; // Снимаем блокировку
                    if (statusText != null) statusText.Text = "🔥 Попадание! Стреляйте снова!";
                    // НЕ вызываем UpdateStatusAndBoards, так как доски уже обновлены в ATTACK_RESULT
                    break;
                case NetworkProtocol.Commands.OpponentTurn:
                    Console.WriteLine("[DEBUG] OPPONENT_TURN received");
                    playerTurn = false;
                    if (statusText != null) statusText.Text = $"💭 Ход соперника ({opponentName})...";
                    // Перерисовываем доски, чтобы кнопки стали некликабельными
                    UpdateStatusAndBoards();
                    break;
                case NetworkProtocol.Commands.OpponentLeft:
                    Console.WriteLine("[DEBUG] OPPONENT_LEFT received");
                    var leftMessage = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Message, "Соперник покинул игру");

                    var messageWindow = new Window
                    {
                        Title = "Соперник покинул игру",
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        Classes = { "Window" }
                    };

                    var messagePanel = new StackPanel
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 20,
                        Margin = new Thickness(20)
                    };

                    var messageText = new TextBlock
                    {
                        Text = leftMessage,
                        Classes = { "Subtitle" },
                        TextAlignment = Avalonia.Media.TextAlignment.Center,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    };

                    var okButton = new Button
                    {
                        Content = "OK",
                        Classes = { "GameButton" },
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    };
                    okButton.Click += (s, e) => 
                    {
                        messageWindow.Close();
                        LeaveNetworkGameAsync(); // Синхронный выход
                        ShowMainMenu();
                    };

                    messagePanel.Children.Add(messageText);
                    messagePanel.Children.Add(okButton);
                    messageWindow.Content = messagePanel;
    
                    // Показываем модальное окно (НЕ ЖДЕМ)
                    messageWindow.ShowDialog(this);
                    break;
                case NetworkProtocol.Commands.AttackResult:
                    // Результат выстрела соперника или игрока
                    Console.WriteLine("[DEBUG] ATTACK_RESULT received");
                    if (int.TryParse(message.Data.GetValueOrDefault(NetworkProtocol.Keys.X, ""), out int x) &&
                        int.TryParse(message.Data.GetValueOrDefault(NetworkProtocol.Keys.Y, ""), out int y))
                    {
                        bool hit = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Hit, "false") == "true";
                        bool sunk = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Sunk, "false") == "true";
                        bool gameOver = message.Data.GetValueOrDefault(NetworkProtocol.Keys.GameOver, "false") == "true";
                        var attackerId = message.Data.GetValueOrDefault(NetworkProtocol.Keys.AttackerId, "");
                        bool isMyAttack = attackerId == myPlayerId; // myPlayerId нужно получить в JOINED

                        Console.WriteLine($"[DEBUG] ATTACK_RESULT: ({x},{y}), hit={hit}, sunk={sunk}, gameOver={gameOver}, isMyAttack={isMyAttack}");

                        if (isMyAttack) // Результат МОЕЙ атаки по сопернику
                        {
                            Console.WriteLine($"[DEBUG] Processing MY attack result at ({x},{y})");
                            
                            // Обновляем доску соперника локально
                            if (hit)
                            {
                                opponentBoard.Grid[x, y] = sunk ? CellState.Sunk : CellState.Hit;
                                playerHits++;
                                SoundManager.PlayHit();
                                
                                if (sunk)
                                {
                                    SoundManager.PlaySunk();
                                    // Обрабатываем потопленный корабль
                                    if (message.Data.ContainsKey(NetworkProtocol.Keys.SunkShipPositions))
                                    {
                                        var positions = message.Data[NetworkProtocol.Keys.SunkShipPositions].Split(',');
                                        foreach (var pos in positions)
                                        {
                                            var coords = pos.Split(':');
                                            if (coords.Length == 2 && 
                                                int.TryParse(coords[0], out int sx) && 
                                                int.TryParse(coords[1], out int sy))
                                            {
                                                opponentBoard.Grid[sx, sy] = CellState.Sunk;
                                            }
                                        }
                        
                                        Console.WriteLine($"[DEBUG] Marked sunk ship positions: {message.Data[NetworkProtocol.Keys.SunkShipPositions]}");
                                    }
                                    // Обрабатываем заблокированные клетки
                                    if (message.Data.ContainsKey(NetworkProtocol.Keys.BlockedCells))
                                    {
                                        var blockedCells = message.Data[NetworkProtocol.Keys.BlockedCells].Split(',');
                                        foreach (var cell in blockedCells)
                                        {
                                            var coords = cell.Split(':');
                                            if (coords.Length == 2 && 
                                                int.TryParse(coords[0], out int bx) && 
                                                int.TryParse(coords[1], out int by))
                                            {
                                                opponentBoard.Grid[bx, by] = CellState.Blocked;
                                            }
                                        }
                        
                                        Console.WriteLine($"[DEBUG] Marked blocked cells: {message.Data[NetworkProtocol.Keys.BlockedCells]}");
                                    }
                                    if (statusText != null)
                                    {
                                        statusText.Text = gameOver ? "🎉 ПОБЕДА!" : "💥 Корабль потоплен! Стреляйте снова!";
                                    }
                                }
                                else
                                {
                                    if (statusText != null)
                                    {
                                        statusText.Text = "🔥 ПОПАДАНИЕ! Стреляйте снова!";
                                    }
                                }
                            }
                            else
                            {
                                opponentBoard.Grid[x, y] = CellState.Miss;
                                playerMisses++;
                                SoundManager.PlayMiss();
                                if (statusText != null)
                                {
                                    statusText.Text = "💧 Промах! Ход переходит к сопернику...";
                                }
                            }
                            
                            UpdateBoard(enemyCanvas, opponentBoard, true);
                            
                            // Снимаем блокировку атаки
                            _isProcessingNetworkAttack = false;
                            Console.WriteLine("[DEBUG] Attack processing flag cleared");
                        }
                        else // Результат атаки СОПЕРНИКА по мне
                        {
                            Console.WriteLine($"[DEBUG] Processing OPPONENT attack result at ({x},{y})");
                            
                            // Обновляем мою доску
                            if (hit)
                            {
                                playerBoard.Grid[x, y] = sunk ? CellState.Sunk : CellState.Hit;
                                opponentHits++;
                                SoundManager.PlayHit();
                                
                                if (sunk)
                                {
                                    SoundManager.PlaySunk();
                                    // Обрабатываем потопленный корабль на моей доске
                                    if (message.Data.ContainsKey(NetworkProtocol.Keys.SunkShipPositions))
                                    {
                                        var positions = message.Data[NetworkProtocol.Keys.SunkShipPositions].Split(',');
                                        foreach (var pos in positions)
                                        {
                                            var coords = pos.Split(':');
                                            if (coords.Length == 2 && 
                                                int.TryParse(coords[0], out int sx) && 
                                                int.TryParse(coords[1], out int sy))
                                            {
                                                playerBoard.Grid[sx, sy] = CellState.Sunk;
                                            }
                                        }
                                    }
                    
                                    // Обрабатываем заблокированные клетки на моей доске
                                    if (message.Data.ContainsKey(NetworkProtocol.Keys.BlockedCells))
                                    {
                                        var blockedCells = message.Data[NetworkProtocol.Keys.BlockedCells].Split(',');
                                        foreach (var cell in blockedCells)
                                        {
                                            var coords = cell.Split(':');
                                            if (coords.Length == 2 && 
                                                int.TryParse(coords[0], out int bx) && 
                                                int.TryParse(coords[1], out int by))
                                            {
                                                playerBoard.Grid[bx, by] = CellState.Blocked;
                                            }
                                        }
                                    }
                                    if (statusText != null)
                                    {
                                        statusText.Text = gameOver ? "💀 ПОРАЖЕНИЕ!" : "⚠️ Противник потопил ваш корабль!";
                                    }
                                }
                                else
                                {
                                    if (statusText != null)
                                    {
                                        statusText.Text = "💥 Противник попал в ваш корабль!";
                                    }
                                }
                            }
                            else
                            {
                                playerBoard.Grid[x, y] = CellState.Miss;
                                opponentMisses++;
                                SoundManager.PlayMiss();
                                if (statusText != null)
                                {
                                    statusText.Text = "Противник промахнулся! Ваш ход!";
                                }
                            }
                            
                            UpdateBoard(ownCanvas, playerBoard, false);
                        }

                        UpdateStats();
                        
                        if (gameOver)
                        {
                            playerTurn = false;
                            if (isMyAttack)
                            {
                                SoundManager.PlayWin();
                            }
                            else
                            {
                                SoundManager.PlayLose();
                            }
                            Console.WriteLine($"[DEBUG] Game over! Winner: {(isMyAttack ? "You" : opponentName)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] Failed to parse ATTACK_RESULT coordinates");
                    }
                    break;
                case NetworkProtocol.Commands.GameOver:
                    Console.WriteLine("[DEBUG] GAME_OVER received");
                    string winnerNameGameOver = message.Data.GetValueOrDefault(NetworkProtocol.Keys.Winner, "Unknown");
                    bool iWon = winnerNameGameOver == playerName;
                    
                    if (statusText != null) 
                    {
                        statusText.Text = iWon 
                            ? "🎉 ПОЗДРАВЛЯЕМ! Вы победили!" 
                            : $"💀 ПОРАЖЕНИЕ! Победил {winnerNameGameOver}";
                    }
                    
                    playerTurn = false;
                    
                    // Создаем окно
                    var gameOverWindow = new Window
                    {
                        Title = iWon ? "Победа!" : "Поражение",
                        Width = 450,
                        Height = 250,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        Classes = { "Window" }
                    };

                    var gameOverPanel = new StackPanel
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 20,
                        Margin = new Thickness(20)
                    };

                    var resultText = new TextBlock
                    {
                        Text = iWon ? "🎉 ПОБЕДА! 🎉" : "💀 ПОРАЖЕНИЕ 💀",
                        Classes = { "Title" },
                        TextAlignment = Avalonia.Media.TextAlignment.Center,
                        FontSize = 32
                    };

                    var winnerText = new TextBlock
                    {
                        Text = iWon ? "Вы потопили весь флот противника!" : $"Победитель: {winnerNameGameOver}",
                        Classes = { "Subtitle" },
                        TextAlignment = Avalonia.Media.TextAlignment.Center
                    };

                    var buttonsPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 15
                    };

                    var newGameButton = new Button
                    {
                        Content = "🔄 Новая игра",
                        Classes = { "GameButton" }
                    };
                    newGameButton.Click += (s, e) => 
                    {
                        gameOverWindow.Close(); // Закрываем окно
                        
                        if (networkMode == NetworkGameMode.InGame)
                        {
                            LeaveNetworkGameAsync(); // Синхронный выход
                            ShowNetworkConnectWindow();
                        }
                        else
                        {
                            StartGame(_lastGameMode);
                        }
                    };

                    var menuButton = new Button
                    {
                        Content = "🏠 В меню",
                        Classes = { "GameButton" }
                    };
                    menuButton.Click += (s, e) => 
                    {
                        gameOverWindow.Close(); // Закрываем окно
                        
                        if (networkMode == NetworkGameMode.InGame)
                        {
                            LeaveNetworkGameAsync(); // Синхронный выход
                            ShowMainMenu();
                        }
                        else
                        {
                            ShowMainMenu();
                        }
                    };

                    buttonsPanel.Children.Add(newGameButton);
                    buttonsPanel.Children.Add(menuButton);
                    gameOverPanel.Children.Add(resultText);
                    gameOverPanel.Children.Add(winnerText);
                    gameOverPanel.Children.Add(buttonsPanel);
                    gameOverWindow.Content = gameOverPanel;
                    
                    // Показываем модальное окно (НЕ ЖДЕМ)
                    gameOverWindow.ShowDialog(this);
                    break;
                case NetworkProtocol.Commands.OpponentDisconnected:
                    Console.WriteLine("[DEBUG] OPPONENT_DISCONNECTED received");

                    var disconnectWindow = new Window
                    {
                        Title = "Соединение потеряно",
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false,
                        Classes = { "Window" }
                    };

                    var disconnectPanel = new StackPanel
                    {
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 20,
                        Margin = new Thickness(20)
                    };

                    var disconnectText = new TextBlock
                    {
                        Text = "Соперник отключился от игры",
                        Classes = { "Subtitle" },
                        TextAlignment = Avalonia.Media.TextAlignment.Center
                    };

                    var disconnectOkButton = new Button
                    {
                        Content = "OK",
                        Classes = { "GameButton" },
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    };
                    disconnectOkButton.Click += (s, e) => 
                    {
                        disconnectWindow.Close();
                        LeaveNetworkGameAsync(); // Синхронный выход
                        ShowMainMenu();
                    };

                    disconnectPanel.Children.Add(disconnectText);
                    disconnectPanel.Children.Add(disconnectOkButton);
                    disconnectWindow.Content = disconnectPanel;

                    // Показываем модальное окно (НЕ ЖДЕМ)
                    disconnectWindow.ShowDialog(this);
                    break;
                case NetworkProtocol.Commands.ChatMessageReceived:
                    HandleChatMessage(message.Data);
                    break;
                case NetworkProtocol.Commands.Error:
                    if (statusText != null) statusText.Text = $"[ERROR] Ошибка сервера: {message.Data.GetValueOrDefault(NetworkProtocol.Keys.Message, "Неизвестная ошибка")}";
                    break;
            }
        // });
    }


    private string myPlayerId = ""; // ID текущего игрока, полученный от сервера

    private void OnNetworkDisconnected()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (statusText != null) statusText.Text = "[WARNING] Соединение с сервером потеряно.";
            networkMode = NetworkGameMode.None;
            myPlayerId = ""; // Сброс ID
            ShowMainMenu(); // Вернуться в меню
        });
    }
    
    /// <summary>
    /// Инициализирует сетевую игру после нахождения соперника.
    /// </summary>
    private void StartNetworkGame()
    {
        chatMessages.Clear();
        currentMode = GameMode.VsPlayer; // Используем существующий режим как основу для сетевой игры
        playerBoard = new GameBoard();
        opponentBoard = new GameBoard(); // Новая доска для соперника
        placingBoard = playerBoard;
        placingPlayer1Ships = true; // Локально - для сетевой игры это всё равно будет "первый игрок" (мы)
        currentShipIndex = 0;
        currentShipHorizontal = true;
        playerTurn = false; // Пока неизвестно, чей ход, сервер сообщит
        isPlayer2Turn = false; // Не используется в сетевой игре
        playerHits = 0;
        playerMisses = 0;
        opponentHits = 0; // Попадания соперника по нам
        opponentMisses = 0; // Промахи соперника
        lastHits.Clear();
        lastHitDirection = null;
        initialHit = null;
        localShipsPlaced = false;
        opponentShipsPlaced = false;
        ShowShipPlacementScreen();
    }
    
    private void HandleChatMessage(Dictionary<string, string> data)
    {
        var sender = data.GetValueOrDefault(NetworkProtocol.Keys.Sender, "Opponent");
        var text = data.GetValueOrDefault(NetworkProtocol.Keys.ChatText, "");
    
        Console.WriteLine($"[Chat] {sender}: {text}");
        chatMessages.Add((sender, text));
    
        // Обновляем UI чата
        UpdateChatDisplay();
    }
    
    private async Task SendChatMessageAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || networkMode != NetworkGameMode.InGame)
            return;
    
        var chatMsg = new NetworkMessage
        {
            Type = NetworkProtocol.Commands.ChatMessage,
            Data = { { NetworkProtocol.Keys.ChatText, text } }
        };
    
        await networkClient.SendMessageAsync(chatMsg);
    
        // Добавляем своё сообщение в чат
        chatMessages.Add(("Вы", text));
        UpdateChatDisplay();
    }
    
    private void UpdateChatDisplay()
    {
        if (chatScrollViewer?.Content is not StackPanel chatPanel)
            return;
    
        chatPanel.Children.Clear();
    
        foreach (var (sender, text) in chatMessages)
        {
            var messageBlock = new TextBlock
            {
                Text = $"{sender}: {text}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(5, 2),
                Foreground = sender == "Вы" 
                    ? new SolidColorBrush(Color.FromRgb(144, 238, 144)) // LightGreen
                    : new SolidColorBrush(Color.FromRgb(173, 216, 230)), // LightBlue
                FontSize = 14
            };
            chatPanel.Children.Add(messageBlock);
        }
    
        // Прокручиваем вниз
        Dispatcher.UIThread.Post(() => 
        {
            if (chatScrollViewer != null)
            {
                chatScrollViewer.ScrollToEnd();
            }
        });
    }
    
    /// <summary>
    /// Обработчик клика по ячейке во время сетевой игры.
    /// </summary>
    private async Task OnNetworkGameCellClickAsync(int x, int y)
    {
        Console.WriteLine($"[DEBUG] OnNetworkGameCellClickAsync called: x={x}, y={y}, playerTurn={playerTurn}, isProcessing={_isProcessingNetworkAttack}");
    
        if (!playerTurn || _isProcessingNetworkAttack)
        {
            Console.WriteLine($"[DEBUG] Attack rejected: playerTurn={playerTurn}, isProcessing={_isProcessingNetworkAttack}");
            return;
        }

        // Проверяем состояние клетки
        var cellState = opponentBoard.Grid[x, y];
        if (cellState != CellState.Empty && cellState != CellState.Ship)
        {
            Console.WriteLine($"[DEBUG] Cell ({x},{y}) already attacked: state={cellState}");
            return;
        }

        _isProcessingNetworkAttack = true;
    
        try
        {
            Console.WriteLine($"[DEBUG] Sending ATTACK message: x={x}, y={y}");
        
            // Отправляем атаку на сервер
            var attackMsg = new NetworkMessage 
            { 
                Type = NetworkProtocol.Commands.Attack, 
                Data = { 
                    { NetworkProtocol.Keys.X, x.ToString() }, 
                    { NetworkProtocol.Keys.Y, y.ToString() } 
                } 
            };
            await networkClient.SendMessageAsync(attackMsg);
        
            // Визуально отмечаем, что атака отправлена (опционально)
            if (statusText != null)
            {
                statusText.Text = "Атака отправлена... Ждем результата...";
            }
        
            Console.WriteLine($"[DEBUG] ATTACK message sent successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Error sending attack: {ex.Message}");
            if (statusText != null)
            {
                statusText.Text = $"Ошибка отправки атаки: {ex.Message}";
            }
            _isProcessingNetworkAttack = false;
        }
        finally
        {
            // Сбрасываем флаг в любом случае (успешно или с ошибкой)
            // Это позволяет снова кликать после завершения операции
            _isProcessingNetworkAttack = false;
        }
    }

    private void ShowMainMenu()
    {
        // Если выходим из сетевой игры, уведомляем сервер
        if (networkMode == NetworkGameMode.InGame && networkClient.IsConnected)
        {
            LeaveNetworkGameAsync(); // Синхронный выход
        }
        else if (networkClient.IsConnected)
        {
            networkClient.Disconnect();
            networkMode = NetworkGameMode.None;
            myPlayerId = "";
        }
    
        currentMode = GameMode.Menu;
        RenderMainMenu();
    }
    
    private void RenderMainMenu()
    {
        currentMode = GameMode.Menu;
    
        var menuPanel = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 20
        };

        var titleText = new TextBlock
        {
            Text = "⚓\uFE0E МОРСКОЙ БОЙ ⚓️\uFE0E",
            Classes = { "Title" }
        };

        var descriptionText = new TextBlock
        {
            Text = "Game created by F.A.S.T DEVELOPMENT",
            Classes = { "Subtitle" }
        };

        var vsComputerBtn = CreateMenuButton("🤖 Играть против компьютера", ShowDifficultyWindow, false);
        var vsPlayerBtn = CreateMenuButton("👥 Играть вдвоём", () => StartGame(GameMode.VsPlayer), false);
        var vsOnlineBtn = CreateMenuButton("🌐 Играть онлайн", ShowNetworkConnectWindow, false);

        menuPanel.Children.Add(titleText);
        menuPanel.Children.Add(vsComputerBtn);
        menuPanel.Children.Add(vsPlayerBtn);
        menuPanel.Children.Add(vsOnlineBtn);
        menuPanel.Children.Add(descriptionText);

        Content = menuPanel;
    }
    
    /// <summary>
    /// Корректный выход из сетевой игры с уведомлением сервера
    /// </summary>
    private void LeaveNetworkGameAsync()
    {
        /*if (networkMode == NetworkGameMode.InGame && networkClient.IsConnected)
        {
            Console.WriteLine("[DEBUG] Leaving network game...");
        
            // Отправляем серверу сообщение о выходе из игры
            try
            {
                var leaveMsg = new NetworkMessage 
                { 
                    Type = NetworkProtocol.Commands.LeaveGame,
                    Data = { { NetworkProtocol.Keys.PlayerId, myPlayerId } }
                };
                await networkClient.SendMessageAsync(leaveMsg);
                Console.WriteLine("[DEBUG] LEAVE_GAME message sent");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error sending LEAVE_GAME: {ex.Message}");
            }
        
            // Небольшая задержка для отправки сообщения
            await Task.Delay(200);
        }
    
        // Отключаемся от сервера
        if (networkClient.IsConnected)
        {
            networkClient.Disconnect();
        }
    
        // Сбрасываем состояние
        networkMode = NetworkGameMode.None;
        myPlayerId = "";
        playerTurn = false;
    
        Console.WriteLine("[DEBUG] Network game left, returning to menu");*/
        Console.WriteLine("[DEBUG] Leaving network game...");
    
        // Просто отключаемся - НЕ ждем ответа от сервера
        if (networkClient.IsConnected)
        {
            try
            {
                // Пытаемся отправить сообщение, но НЕ ждем подтверждения
                var leaveMsg = new NetworkMessage 
                { 
                    Type = NetworkProtocol.Commands.LeaveGame,
                    Data = { { NetworkProtocol.Keys.PlayerId, myPlayerId } }
                };
                _ = networkClient.SendMessageAsync(leaveMsg); // Fire and forget
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error sending LEAVE_GAME: {ex.Message}");
            }
        
            // Сразу отключаемся
            networkClient.Disconnect();
        }
    
        // Сбрасываем состояние
        networkMode = NetworkGameMode.None;
        myPlayerId = "";
        playerTurn = false;
    
        Console.WriteLine("[DEBUG] Network game left");
    }

    private void ShowDifficultyWindow()
    {
        var window = new Window
        {
            Title = "Выбор сложности",
            Width = 400,
            Height = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            // Background = new SolidColorBrush(Color.FromRgb(20, 30, 50))
            Classes = { "Window" }
        };

        var panel = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 20,
            Margin = new Thickness(20)
        };

        var title = new TextBlock
        {
            Text = "Выберите сложность бота",
            Classes = { "DifficultyTitle" }
        };

        var easyBtn = CreateDifficultyButton("Лёгкий (рандом)", BotDifficulty.Easy, () => StartWithBot(window, BotDifficulty.Easy));
        var mediumBtn = CreateDifficultyButton("Средний (умнее)", BotDifficulty.Medium, () => StartWithBot(window, BotDifficulty.Medium));
        var hardBtn = CreateDifficultyButton("Сложный (ИИ)", BotDifficulty.Hard, () => StartWithBot(window, BotDifficulty.Hard));

        panel.Children.Add(title);
        panel.Children.Add(easyBtn);
        panel.Children.Add(mediumBtn);
        panel.Children.Add(hardBtn);

        window.Content = panel;
        window.ShowDialog(this); // Открываем как модальное окно
    }
    
    // --- НОВОЕ МЕНЮ ДЛЯ ПОДКЛЮЧЕНИЯ ---
    private void ShowNetworkConnectWindow()
    {
        var window = new Window
        {
            Title = "Подключение к серверу",
            Width = 400,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Classes = { "Window" }
        };

        var panel = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 15,
            Margin = new Thickness(20)
        };

        var title = new TextBlock
        {
            Text = "Введите данные сервера",
            Classes = { "Subtitle" }
        };

        var nameInput = new TextBox
        {
            Watermark = "Ваш ник",
            Text = playerName // Подставляем текущее имя
        };
        var hostInput = new TextBox
        {
            Watermark = "IP-адрес сервера",
            Text = "127.0.0.1" // Значение по умолчанию
        };
        var portInput = new TextBox
        {
            Watermark = "Порт (8889)",
            Text = "8889" // Значение по умолчанию
        };

        var connectBtn = new Button();
        connectBtn = CreateMenuButton("Подключиться", async () =>
        {
            playerName = nameInput.Text ?? "Anon";
            string hostname = hostInput.Text ?? "127.0.0.1";
            int port = 8889;
            if (int.TryParse(portInput.Text, out int parsedPort) && parsedPort > 0 && parsedPort < 65536)
            {
                port = parsedPort;
            }
            else
            {
                var errorTextBlock = panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "ConnectionErrorTextBlock");
                if (errorTextBlock == null)
                {
                    errorTextBlock = new TextBlock
                    {
                        Name = "ConnectionErrorTextBlock", 
                        Foreground = Brushes.Red, 
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    // Вставим его после кнопки подключения
                    int connectBtnIndex = panel.Children.IndexOf(connectBtn);
                    panel.Children.Insert(connectBtnIndex + 1, errorTextBlock);
                }
                errorTextBlock.Text = "Неверный порт.";
                return; // Не пытаемся подключаться
            }

            var (success, errorMessage) = await ConnectToServer(hostname, port);

            var errorTextBlockExisting = panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Name == "ConnectionErrorTextBlock");
            if (!success)
            {
                if (errorTextBlockExisting == null)
                {
                    errorTextBlockExisting = new TextBlock
                    {
                        Name = "ConnectionErrorTextBlock", 
                        Foreground = Brushes.Red, 
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    int connectBtnIndex = panel.Children.IndexOf(connectBtn);
                    panel.Children.Insert(connectBtnIndex + 1, errorTextBlockExisting);
                }
                errorTextBlockExisting.Text = errorMessage;
            }
            else
            {
                if (errorTextBlockExisting != null)
                {
                    panel.Children.Remove(errorTextBlockExisting); // Убираем сообщение об ошибке, если успешно
                }
                window.Close(); // Закрываем окно подключения
                // Остаемся на главном окне, но состояние изменено на ожидание
                // Можно обновить статус в главном окне, если нужно, но только если статус-панель видима
                if (statusText != null) // Проверка на null
                {
                    statusText.Text = $"Подключение к серверу... Ищу соперника..."; // Теперь это безопасно
                }
                // Но лучше отправить это в OnNetworkMessageReceived, как и раньше, когда придет JOINED или MATCH_FOUND
                // Поэтому, возможно, просто оставим пустое или обновим в JOINED
            }
        }, true);
        var backBtn = CreateMenuButton("Назад", () => window.Close(), true);

        panel.Children.Add(title);
        panel.Children.Add(nameInput);
        panel.Children.Add(hostInput);
        panel.Children.Add(portInput);
        panel.Children.Add(connectBtn);
        panel.Children.Add(backBtn);

        window.Content = panel;
        window.ShowDialog(this);
    }
    
    private void StartWithBot(Window difficultyWindow, BotDifficulty difficulty)
    {
        botDifficulty = difficulty;
        difficultyWindow.Close();
        StartGame(GameMode.VsComputer);
    }

    private Button CreateMenuButton(string text, Action onClick, bool sub)
    {
        var button = new Button
        {
            Content = text,
            Classes = { sub ? "SubButton" : "MenuButton" }
        };
        button.Click += (s, e) => onClick();
        return button;
    }

    private Button CreateDifficultyButton(string text, BotDifficulty difficulty, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Classes = {"DifficultyButton", "GameButton"}
        };
        button.Classes.Add(difficulty switch
        {
            BotDifficulty.Easy => "DifficultyEasy",
            BotDifficulty.Medium => "DifficultyMedium",
            BotDifficulty.Hard => "DifficultyHard",
            _ => "DifficultyEasy"
        });
        button.Click += (s, e) => onClick();
        return button;
    }

    private void StartGame(GameMode mode)
    {
        _lastGameMode = mode;
        if (networkMode != NetworkGameMode.None) return;
        
        currentMode = mode;
        playerBoard = new GameBoard();
        computerBoard = new GameBoard();
        opponentBoard = null;
        placingBoard = playerBoard;
        placingPlayer1Ships = true;
        currentShipIndex = 0;
        currentShipHorizontal = true;
        playerTurn = true;
        isPlayer2Turn = false;
        playerHits = 0;
        playerMisses = 0;
        computerHits = 0;
        computerMisses = 0;
        opponentHits = 0;
        opponentMisses = 0;
        lastHits.Clear();
        lastHitDirection = null;
        initialHit = null;
        ShowShipPlacementScreen();
    }

    private void ShowShipPlacementScreen()
    {
        var placementPanel = new StackPanel
        {
            Margin = new Thickness(20),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        string playerName = "Игрок";
        if (currentMode == GameMode.VsPlayer && networkMode == NetworkGameMode.None)
        {
            playerName = placingPlayer1Ships ? "Игрок 1" : "Игрок 2";
        }
        else if (networkMode == NetworkGameMode.InGame)
        {
            playerName = "Вы";
        }

        var titleBorder = new Border
        {
            Classes = { "Panel" },
            Margin = new Thickness(0, 0, 0, 20)
        };

        statusText = new TextBlock
        {
            Text = $"🚢 {playerName}: Расставьте корабли",
            Classes = { "Status" }
        };
        titleBorder.Child = statusText;

        var instructionText = new TextBlock
        {
            Text = currentShipIndex < shipsToPlace.Count
                ? $"Размещаем корабль размером {shipsToPlace[currentShipIndex]} клеток\nПробел - повернуть, ЛКМ - разместить"
                : "Все корабли размещены!",
            Classes = { "Instruction" },
            Margin = new Thickness(0, 0, 0, 20)
        };

        placementCanvas = CreatePlacementCanvas();

        var buttonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 20,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var rotateBtn = new Button
        {
            Content = "🔄 Повернуть (Пробел)",
            Classes = { "GameButton", "RotateButton"}
        };
        rotateBtn.Click += (s, e) => RotateCurrentShip();

        var randomBtn = new Button
        {
            Content = "🎲 Случайная расстановка",
            Classes = { "GameButton", "RandomButton" }
        };
        randomBtn.Click += (s, e) => PlaceShipsRandomly();

        var startBtn = new Button
        {
            Content = "▶️ Начать игру",
            Classes = { "GameButton", "StartButton" },
            IsEnabled = currentShipIndex >= shipsToPlace.Count // Начальное состояние
        };
        startBtn.Click += (s, e) => FinishPlacement();

        buttonsPanel.Children.Add(rotateBtn);
        buttonsPanel.Children.Add(randomBtn);
        buttonsPanel.Children.Add(startBtn);

        placementPanel.Children.Add(titleBorder);
        placementPanel.Children.Add(instructionText);
        placementPanel.Children.Add(placementCanvas);
        placementPanel.Children.Add(buttonsPanel);

        Content = placementPanel;

        KeyDown += OnPlacementKeyDown;
    }

    private Canvas CreatePlacementCanvas()
    {
        var canvas = new Canvas
        {
            Classes = { "GameBoard" }
        };

        int cellSize = 40;
        int padding = 10;

        for (int i = 0; i < placingBoard.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString(),
                Classes = { "Coordinate" }
            };
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString(),
                Classes = { "Coordinate" }
            };
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize / 2 - 7);
            canvas.Children.Add(numberText);
        }

        for (int i = 0; i < placingBoard.Size; i++)
        {
            for (int j = 0; j < placingBoard.Size; j++)
            {
                var cell = CreatePlacementCell(i, j, cellSize);
                Canvas.SetLeft(cell, padding + i * cellSize);
                Canvas.SetTop(cell, padding + j * cellSize);
                canvas.Children.Add(cell);
            }
        }

        return canvas;
    }

    private Control CreatePlacementCell(int x, int y, int cellSize)
    {
        var border = new Border
        {
            Width = cellSize - 2,
            Height = cellSize - 2,
            Classes = { "PlacementCell" }
        };

        if (placingBoard.Grid[x, y] == CellState.Ship)
        {
            border.Classes.Add("Ship");
            var content = new Canvas { Width = cellSize - 2, Height = cellSize - 2 };
            DrawShipSegment(content, cellSize - 2);
            border.Child = content;
        }
        else
        {
            border.Classes.Add("Empty");
        }

        int cx = x, cy = y;
        border.PointerPressed += (s, e) => OnPlacementCellClick(cx, cy);

        border.PointerEntered += (s, e) =>
        {
            if (currentShipIndex < shipsToPlace.Count)
            {
                HighlightShipPlacement(x, y, true);
            }
        };

        border.PointerExited += (s, e) =>
        {
            if (currentShipIndex < shipsToPlace.Count)
            {
                HighlightShipPlacement(x, y, false);
            }
        };

        return border;
    }

    private void HighlightShipPlacement(int x, int y, bool highlight)
    {
        if (currentShipIndex >= shipsToPlace.Count) return;

        int shipSize = shipsToPlace[currentShipIndex];
        bool canPlace = placingBoard.CanPlaceShip(x, y, shipSize, currentShipHorizontal);

        for (int i = 0; i < shipSize; i++)
        {
            int px = currentShipHorizontal ? x + i : x;
            int py = currentShipHorizontal ? y : y + i;

            if (px >= 0 && px < placingBoard.Size && py >= 0 && py < placingBoard.Size)
            {
                var border = FindCellBorder(px, py);
                if (border != null && placingBoard.Grid[px, py] != CellState.Ship)
                {
                    border.Classes.Remove("CanPlace");
                    border.Classes.Remove("CannotPlace");
                    border.Classes.Remove("Empty");
                    if (highlight)
                    {
                        border.Classes.Add(canPlace ? "CanPlace" : "CannotPlace");
                    }
                    else
                    {
                        border.Classes.Add("Empty");
                    }
                }
            }
        }
    }

    private Border FindCellBorder(int x, int y)
    {
        int cellSize = 40;
        int padding = 10;

        foreach (var child in placementCanvas.Children)
        {
            if (child is Border border)
            {
                double left = Canvas.GetLeft(border);
                double top = Canvas.GetTop(border);

                if (Math.Abs(left - (padding + x * cellSize)) < 1 &&
                    Math.Abs(top - (padding + y * cellSize)) < 1)
                {
                    return border;
                }
            }
        }
        return null;
    }

    private void OnPlacementCellClick(int x, int y)
    {
        if (currentShipIndex >= shipsToPlace.Count) return;

        int shipSize = shipsToPlace[currentShipIndex];
        var ship = new Ship(shipSize, currentShipHorizontal);

        if (placingBoard.PlaceShip(ship, x, y))
        {
            currentShipIndex++;
            RefreshPlacementCanvas();

            if (currentShipIndex >= shipsToPlace.Count)
            {
                statusText.Text = "✅ Все корабли размещены! Нажмите 'Начать игру'";
                EnableStartButton();
            }
        }
    }

    private void PlaceShipsRandomly()
    {
        placingBoard.Clear();
        placingBoard.PlaceShipsRandomly();
        currentShipIndex = shipsToPlace.Count;
        RefreshPlacementCanvas();
        statusText.Text = "✅ Все корабли размещены! Нажмите 'Начать игру'";
        EnableStartButton();
    }

    private void EnableStartButton()
    {
        if (Content is StackPanel mainPanel)
        {
            foreach (var child in mainPanel.Children)
            {
                if (child is StackPanel buttonPanel)
                {
                    foreach (var button in buttonPanel.Children)
                    {
                        if (button is Button btn && btn.Content.ToString().Contains("Начать игру"))
                        {
                            btn.IsEnabled = true;
                            return;
                        }
                    }
                }
            }
        }
    }

    private void OnPlacementKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            RotateCurrentShip();
        }
    }

    private void RotateCurrentShip()
    {
        currentShipHorizontal = !currentShipHorizontal;
    }


    private void RefreshPlacementCanvas()
    {
        var parent = placementCanvas.Parent as Panel;
        int index = parent.Children.IndexOf(placementCanvas);
        parent.Children.RemoveAt(index);
        placementCanvas = CreatePlacementCanvas();
        parent.Children.Insert(index, placementCanvas);
    }

    private async void FinishPlacement()
    {
        KeyDown -= OnPlacementKeyDown;

        if (currentMode == GameMode.VsPlayer && networkMode == NetworkGameMode.None && placingPlayer1Ships)
        {
            // Сохраняем корабли первого игрока
            placingPlayer1Ships = false;
            placingBoard = computerBoard;
            currentShipIndex = 0;
            currentShipHorizontal = true;
            ShowShipPlacementScreen();
        }
        else
        {
            // Начинаем игру
            if (currentMode == GameMode.VsComputer)
            {
                computerBoard.PlaceShipsRandomly();
                ShowGameScreen();
            }
            else if (networkMode == NetworkGameMode.InGame)
            {
                // Сетевая игра: отправляем серверу информацию о кораблях
                localShipsPlaced = true;
            
                // Отправляем расположение кораблей (опционально - для валидации на сервере)
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
                await networkClient.SendMessageAsync(placementMsg);
            
                // Сообщаем, что расстановка завершена
                var readyMsg = new NetworkMessage { Type = NetworkProtocol.Commands.AllShipsPlaced };
                await networkClient.SendMessageAsync(readyMsg);
            
                if (statusText != null)
                {
                    statusText.Text = "Корабли расставлены! Ждем соперника...";
                }
                return;
            }
            else
            {
                // Локальная игра 1 на 1 (второй игрок)
                ShowGameScreen();
            }
        }
    }


    private void ShowGameScreen()
    {
        //if (currentMode == GameMode.VsComputer)
        //{
        //    computerBoard.PlaceShipsRandomly();
        //}
        // playerTurn = true;  // ← На всякий случай
        Console.WriteLine($"[DEBUG] ShowGameScreen called, current playerTurn is: {playerTurn}");
        isPlayer2Turn = false;
        ownCanvas = new Canvas { Classes = { "GameBoard" } };
        enemyCanvas = new Canvas { Classes = { "GameBoard" } };
        chatMessages.Clear();

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        var titlePanel = new Border
        {
            Classes = { "Panel" },
            Margin = new Thickness(0, 0, 0, 20)
        };

        string statusMessage = "";
        if (networkMode != NetworkGameMode.InGame)
        {
            if (currentMode == GameMode.VsPlayer)
            {
                statusMessage = isPlayer2Turn ? "⚔️ ВАШ ХОД, ИГРОК 2! Атакуйте поле противника" : "⚔️ ВАШ ХОД, ИГРОК 1! Атакуйте поле противника";
            }
            else
            {
                statusMessage = "⚔️ ВАШ ХОД! Атакуйте поле противника";
            }
        }
        else if (networkMode == NetworkGameMode.InGame)
        {
            // statusMessage = "Ждем начала игры от сервера...";
            statusMessage = "";
        }

        statusText = new TextBlock
        {
            Text = statusMessage,
            Classes = { "Status" }
        };
        titlePanel.Child = statusText;
        
        var contentPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 20
        };

        var boardsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 50
        };

        var ownPanel = CreateBoardPanel(null, false, "🛡️ ВАШЕ ПОЛЕ", ownCanvas);
        var enemyPanel = CreateBoardPanel(null, true, "🎯 ПОЛЕ ПРОТИВНИКА", enemyCanvas);

        boardsPanel.Children.Add(ownPanel);
        boardsPanel.Children.Add(enemyPanel);
        
        contentPanel.Children.Add(boardsPanel);

        // ДОБАВЛЯЕМ ЧАТ только для сетевой игры
        if (networkMode == NetworkGameMode.InGame)
        {
            var chatPanel = CreateChatPanel();
            contentPanel.Children.Add(chatPanel);
        }

        var buttonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 20,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var resetButton = new Button
        {
            Content = "🔄 НОВАЯ ИГРА",
            Classes = { "GameButton", "ResetButton" }
        };
        resetButton.Click += (s, e) => 
        {
            if (networkMode == NetworkGameMode.InGame)
            {
                // Сетевая игра - показываем подтверждение
                ShowConfirmDialog(
                    "Начать новую онлайн-игру?\nТекущая игра будет завершена.",
                    () => {
                        LeaveNetworkGame();
                        ShowNetworkConnectWindow();
                    }
                );
                /*var confirmWindow = new Window
                {
                    Title = "Подтверждение",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Classes = { "Window" }
                };

                var confirmPanel = new StackPanel
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 20,
                    Margin = new Thickness(20)
                };

                var confirmText = new TextBlock
                {
                    Text = "Начать новую онлайн-игру?\nТекущая игра будет завершена.",
                    Classes = { "Subtitle" },
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };

                var confirmButtonsPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 15
                };

                var yesButton = new Button
                {
                    Content = "Да",
                    Classes = { "GameButton" }
                };
                yesButton.Click += (ss, ee) => 
                {
                    confirmWindow.Close();
                    LeaveNetworkGameAsync(); // Синхронный выход
                    ShowNetworkConnectWindow();
                };

                var noButton = new Button
                {
                    Content = "Нет",
                    Classes = { "GameButton" }
                };
                noButton.Click += (ss, ee) => confirmWindow.Close();

                confirmButtonsPanel.Children.Add(yesButton);
                confirmButtonsPanel.Children.Add(noButton);
                confirmPanel.Children.Add(confirmText);
                confirmPanel.Children.Add(confirmButtonsPanel);
                confirmWindow.Content = confirmPanel;
                confirmWindow.ShowDialog(this);*/
            }
            else
            {
                // Локальная игра - просто начинаем заново
                StartGame(currentMode);
            }
        };

        var menuButton = new Button
        {
            Content = "🏠 В МЕНЮ",
            Classes = { "GameButton", "MenuExitButton" }
        };
        menuButton.Click += (s, e) => 
        {
            if (networkMode == NetworkGameMode.InGame)
            {
                // Сетевая игра - показываем подтверждение
                ShowConfirmDialog(
                    "Вернуться в главное меню?\nТекущая игра будет завершена.",
                    () => {
                        LeaveNetworkGame();
                        ShowMainMenu();
                    }
                );
                /*var confirmWindow = new Window
                {
                    Title = "Подтверждение",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false,
                    Classes = { "Window" }
                };

                var confirmPanel = new StackPanel
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 20,
                    Margin = new Thickness(20)
                };

                var confirmText = new TextBlock
                {
                    Text = "Вернуться в главное меню?\nТекущая игра будет завершена.",
                    Classes = { "Subtitle" },
                    TextAlignment = Avalonia.Media.TextAlignment.Center,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                };

                var confirmButtonsPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Spacing = 15
                };

                var yesButton = new Button
                {
                    Content = "Да",
                    Classes = { "GameButton" }
                };
                yesButton.Click += (ss, ee) => 
                {
                    confirmWindow.Close();
                    LeaveNetworkGameAsync(); // Синхронный выход
                    ShowMainMenu();
                };

                var noButton = new Button
                {
                    Content = "Нет",
                    Classes = { "GameButton" }
                };
                noButton.Click += (ss, ee) => confirmWindow.Close();

                confirmButtonsPanel.Children.Add(yesButton);
                confirmButtonsPanel.Children.Add(noButton);
                confirmPanel.Children.Add(confirmText);
                confirmPanel.Children.Add(confirmButtonsPanel);
                confirmWindow.Content = confirmPanel;
                confirmWindow.ShowDialog(this);*/
            }
            else
            {
                // Локальная игра - просто возвращаемся в меню
                ShowMainMenu();
            }
        };

        buttonsPanel.Children.Add(resetButton);
        buttonsPanel.Children.Add(menuButton);

        mainPanel.Children.Add(titlePanel);
        mainPanel.Children.Add(contentPanel);
        mainPanel.Children.Add(buttonsPanel);

        Content = mainPanel;
        UpdateStatusAndBoards();
    }
    
    private void LeaveNetworkGame()
    {
        Console.WriteLine("[DEBUG] Leaving network game...");
    
        // Просто отключаемся - НЕ ждем ответа от сервера
        if (networkClient.IsConnected)
        {
            try
            {
                // Пытаемся отправить сообщение, но НЕ ждем подтверждения
                var leaveMsg = new NetworkMessage 
                { 
                    Type = NetworkProtocol.Commands.LeaveGame,
                    Data = { { NetworkProtocol.Keys.PlayerId, myPlayerId } }
                };
                _ = networkClient.SendMessageAsync(leaveMsg); // Fire and forget
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error sending LEAVE_GAME: {ex.Message}");
            }
        
            // Сразу отключаемся
            networkClient.Disconnect();
        }
    
        // Сбрасываем состояние
        networkMode = NetworkGameMode.None;
        myPlayerId = "";
        playerTurn = false;
    
        Console.WriteLine("[DEBUG] Network game left");
    }
    
    private StackPanel CreateChatPanel()
    {
        var chatContainer = new StackPanel
        {
            Width = 300,
            Spacing = 10
        };

        // Заголовок чата
        var chatHeader = new Border
        {
            Classes = { "BoardHeader" },
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 8)
        };
        var chatLabel = new TextBlock
        {
            Text = "💬 ЧАТ",
            Classes = { "BoardLabel" }
        };
        chatHeader.Child = chatLabel;

        // Область сообщений
        var messagesPanel = new StackPanel
        {
            Spacing = 5
        };

        chatScrollViewer = new ScrollViewer
        {
            Height = 350,
            Content = messagesPanel,
            Background = new SolidColorBrush(Color.FromRgb(30, 40, 60)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10)
        };

        // Поле ввода
        chatInputBox = new TextBox
        {
            Watermark = "Напишите сообщение...",
            MaxLength = 200,
            Height = 35,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
    
        chatInputBox.KeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(chatInputBox.Text))
            {
                _ = SendChatMessageAsync(chatInputBox.Text);
                chatInputBox.Text = "";
            }
        };

        // Кнопка отправки
        var sendButton = new Button
        {
            Content = "📤 Отправить",
            Classes = { "GameButton" },
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        sendButton.Click += async (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(chatInputBox.Text))
            {
                await SendChatMessageAsync(chatInputBox.Text);
                chatInputBox.Text = "";
                chatInputBox.Focus(); // Возвращаем фокус на поле ввода
            }
        };

        chatContainer.Children.Add(chatHeader);
        chatContainer.Children.Add(chatScrollViewer);
        chatContainer.Children.Add(chatInputBox);
        chatContainer.Children.Add(sendButton);

        return chatContainer;
    }
    
    private void ShowConfirmDialog(string message, Action onConfirm)
    {
        var confirmWindow = new Window
        {
            Title = "Подтверждение",
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Classes = { "Window" }
        };

        var confirmPanel = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 20,
            Margin = new Thickness(20)
        };

        var confirmText = new TextBlock
        {
            Text = message,
            Classes = { "Subtitle" },
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var confirmButtonsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 15
        };

        var yesButton = new Button
        {
            Content = "Да",
            Classes = { "GameButton" }
        };
        yesButton.Click += (s, e) => 
        {
            confirmWindow.Close();
            onConfirm();
        };

        var noButton = new Button
        {
            Content = "Нет",
            Classes = { "GameButton" }
        };
        noButton.Click += (s, e) => confirmWindow.Close();

        confirmButtonsPanel.Children.Add(yesButton);
        confirmButtonsPanel.Children.Add(noButton);
        confirmPanel.Children.Add(confirmText);
        confirmPanel.Children.Add(confirmButtonsPanel);
        confirmWindow.Content = confirmPanel;
    
        confirmWindow.ShowDialog(this);
    }

    private Panel CreateBoardPanel(GameBoard board, bool isEnemy, string title, Canvas canvas)
    {
        var panel = new StackPanel { Spacing = 10 };

        var header = new Border
        {
            Classes = { "BoardHeader" },
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 8)
        };
        header.Classes.Add(isEnemy ? "Enemy" : "Player");
        var label = new TextBlock
        {
            Text = title,
            Classes = { "BoardLabel" }
        };
        header.Child = label;

        var statsText = new TextBlock
        {
            Classes = { "Stats" }
        };

        if (isEnemy)
        {
            if (currentMode == GameMode.VsComputer || (currentMode == GameMode.VsPlayer && networkMode == NetworkGameMode.None))
            {
                computerStatsText = statsText;
                computerStatsText.Text = "💣 Статистика противника: 0 попаданий, 0 промахов";
            }
            else if (networkMode == NetworkGameMode.InGame)
            {
                opponentStatsText = statsText;
                opponentStatsText.Text = $"💣 Выстрелы {opponentName}: 0 попаданий, 0 промахов";
            }
        }
        else
        {
            playerStatsText = statsText;
            playerStatsText.Text = "🎯 Ваши выстрелы: 0 попаданий, 0 промахов";
        }

        panel.Children.Add(header);
        panel.Children.Add(canvas);
        panel.Children.Add(statsText);

        return panel;
    }

    private Control CreateGameCell(GameBoard board, int x, int y, int cellSize, bool isEnemy)
    {
        var border = new Border
        {
            Width = cellSize - 2,
            Height = cellSize - 2,
            Classes = { "GameCell" },
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 2,
                Blur = 4,
                Color = Color.FromArgb(100, 0, 0, 0)
            })
        };

        var state = board.Grid[x, y];
        if (isEnemy && networkMode == NetworkGameMode.InGame && state == CellState.Ship)
        {
            border.Classes.Add("Empty");
        }
        else
        {
            border.Classes.Add(state switch
            {
                CellState.Empty => "Empty",
                CellState.Ship => isEnemy ? "Empty" : "Ship",
                CellState.Miss => "Miss",
                CellState.Hit => "Hit",
                CellState.Sunk => "Sunk",
                CellState.Blocked => "Blocked",
                _ => "Empty"
            });
        }

        var content = new Canvas { Width = cellSize - 2, Height = cellSize - 2 };

        if (board.Grid[x, y] == CellState.Ship && !isEnemy)
        {
            DrawShipSegment(content, cellSize - 2);
        }
        else if (board.Grid[x, y] == CellState.Miss)
        {
            DrawMiss(content, cellSize - 2);
        }
        else if (board.Grid[x, y] == CellState.Hit)
        {
            DrawHit(content, cellSize - 2);
        }
        else if (board.Grid[x, y] == CellState.Sunk)
        {
            DrawSunk(content, cellSize - 2);
        }
        else if (board.Grid[x, y] == CellState.Blocked)
        {
            DrawBlocked(content, cellSize - 2);
        }

        border.Child = content;

        if (isEnemy)
        {
            int cx = x, cy = y;
            bool canClick = false;
            if (networkMode == NetworkGameMode.InGame)
            {
                // Сетевая игра: клик по полю соперника возможен только если мой ход
                canClick = playerTurn; // playerTurn отражает чей ход в сетевой игре
                Console.WriteLine($"[DEBUG] Cell ({cx},{cy}) - networkMode=InGame, playerTurn={playerTurn}, canClick={canClick}");
            }
            else if (currentMode == GameMode.VsPlayer && networkMode == NetworkGameMode.None)
            {
                // Локальная игра 1 на 1: клик по полю соперника возможен, если сейчас мой ход
                // playerTurn в VsPlayer устанавливается в true перед обработкой хода, и false после промаха/попадания
                // Это отражает, готов ли игрок к совершению хода в данный момент
                canClick = playerTurn;
            }
            else if (currentMode == GameMode.VsComputer)
            {
                // Игра против компа: клик по полю компа возможен, если мой ход
                canClick = playerTurn;
            }

            // Объединим проверку canClick и доступности клетки для выстрела
            var cellState = board.Grid[cx, cy];
            bool cellAvailable = cellState == CellState.Empty || cellState == CellState.Ship;
        
            Console.WriteLine($"[DEBUG] Cell ({cx},{cy}) - state={cellState}, cellAvailable={cellAvailable}, finalClick={canClick && cellAvailable}");

            if (canClick && cellAvailable)
            {
                border.PointerPressed += async (s, e) => 
                {
                    Console.WriteLine($"[DEBUG] Cell ({cx},{cy}) CLICKED! networkMode={networkMode}");
                    if (networkMode == NetworkGameMode.InGame)
                    {
                        await OnNetworkGameCellClickAsync(cx, cy);
                    }
                    else
                    {
                        OnGameCellClick(cx, cy);
                    }
                };
                border.Cursor = new Cursor(StandardCursorType.Hand);
            
                // Hover эффект
                border.PointerEntered += (s, e) =>
                {
                    if (cellState == CellState.Empty || cellState == CellState.Ship)
                    {
                        border.Opacity = 0.8;
                    }
                };
                border.PointerExited += (s, e) =>
                {
                    border.Opacity = 1.0;
                };
            }
        }

        return border;
    }

    private void DrawShipSegment(Canvas canvas, int size)
    {
        var ship = new Ellipse
        {
            Width = size * 0.7,
            Height = size * 0.7,
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                    {
                        new GradientStop(Color.FromRgb(100, 100, 100), 0),
                        new GradientStop(Color.FromRgb(60, 60, 60), 1)
                    }
            }
        };
        Canvas.SetLeft(ship, size * 0.15);
        Canvas.SetTop(ship, size * 0.15);
        canvas.Children.Add(ship);
    }

    private void DrawMiss(Canvas canvas, int size)
    {
        var circle = new Ellipse
        {
            Width = size * 0.3,
            Height = size * 0.3,
            Fill = new SolidColorBrush(Color.FromRgb(100, 150, 200))
        };
        Canvas.SetLeft(circle, size * 0.35);
        Canvas.SetTop(circle, size * 0.35);
        canvas.Children.Add(circle);
    }

    private void DrawHit(Canvas canvas, int size)
    {
        var line1 = new Line
        {
            StartPoint = new Point(size * 0.2, size * 0.2),
            EndPoint = new Point(size * 0.8, size * 0.8),
            Stroke = Brushes.Yellow,
            StrokeThickness = 3
        };
        var line2 = new Line
        {
            StartPoint = new Point(size * 0.8, size * 0.2),
            EndPoint = new Point(size * 0.2, size * 0.8),
            Stroke = Brushes.Yellow,
            StrokeThickness = 3
        };
        canvas.Children.Add(line1);
        canvas.Children.Add(line2);
    }

    private void DrawSunk(Canvas canvas, int size)
    {
        var line1 = new Line
        {
            StartPoint = new Point(size * 0.2, size * 0.2),
            EndPoint = new Point(size * 0.8, size * 0.8),
            Stroke = Brushes.Red,
            StrokeThickness = 4
        };
        var line2 = new Line
        {
            StartPoint = new Point(size * 0.8, size * 0.2),
            EndPoint = new Point(size * 0.2, size * 0.8),
            Stroke = Brushes.Red,
            StrokeThickness = 4
        };
        canvas.Children.Add(line1);
        canvas.Children.Add(line2);
    }

    private void DrawBlocked(Canvas canvas, int size)
    {
        var dot = new Ellipse
        {
            Width = size * 0.15,
            Height = size * 0.15,
            Fill = new SolidColorBrush(Color.FromRgb(80, 100, 130))
        };
        Canvas.SetLeft(dot, size * 0.425);
        Canvas.SetTop(dot, size * 0.425);
        canvas.Children.Add(dot);
    }

    private Brush GetCellBrush(CellState state, bool isEnemy)
    {
        return state switch
        {
            CellState.Empty => new SolidColorBrush(Color.FromRgb(50, 80, 120)),
            CellState.Ship => isEnemy
                ? new SolidColorBrush(Color.FromRgb(50, 80, 120))
                : new SolidColorBrush(Color.FromRgb(70, 100, 140)),
            CellState.Miss => new SolidColorBrush(Color.FromRgb(60, 100, 150)),
            CellState.Hit => new SolidColorBrush(Color.FromRgb(180, 120, 40)),
            CellState.Sunk => new SolidColorBrush(Color.FromRgb(150, 50, 50)),
            CellState.Blocked => new SolidColorBrush(Color.FromRgb(40, 60, 90)),
            _ => new SolidColorBrush(Color.FromRgb(50, 80, 120))
        };
    }

    private async void OnGameCellClick(int x, int y)
    {
        if (networkMode != NetworkGameMode.None) return;
        if (currentMode == GameMode.VsPlayer)
        {
            enemyCanvas.IsEnabled = false;

            if (!playerTurn) return;
            try
            {
                GameBoard targetBoard = (currentMode == GameMode.VsPlayer && isPlayer2Turn) ? playerBoard : computerBoard;
                var (hit, sunk, gameOver) = targetBoard.Attack(x, y);

                if (targetBoard.Grid[x, y] == CellState.Miss ||
                    targetBoard.Grid[x, y] == CellState.Hit ||
                    targetBoard.Grid[x, y] == CellState.Sunk)
                {
                    if (hit)
                    {
                        (isPlayer2Turn ? ref computerHits : ref playerHits)++;

                        SoundManager.PlayHit();

                        if (sunk)
                        {
                            SoundManager.PlaySunk();
                            statusText.Text = gameOver
                                ? $"🎉🏆️ ПОБЕДА! {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} потопил весь флот!"
                                : $"💥 {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} потопил корабль!";

                            if (gameOver)
                            {
                                if (isPlayer2Turn)
                                    SoundManager.PlayLose();
                                else
                                    SoundManager.PlayWin();
                                playerTurn = false;
                                UpdateBoard(enemyCanvas, targetBoard, true);
                                UpdateStats();
                                return;
                            }
                        }
                        else
                        {
                            statusText.Text = $"🔥 {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} попал! Стреляет снова!";
                        }
                        UpdateBoard(enemyCanvas, targetBoard, true);
                        UpdateStats();
                        await Task.Delay(500);
                        return;
                    }
                    else if (targetBoard.Grid[x, y] == CellState.Miss)
                    {
                        (isPlayer2Turn ? ref computerMisses : ref playerMisses)++;

                        SoundManager.PlayMiss();
                        statusText.Text = $"💧 {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} промахнулся! Ход переходит к {(isPlayer2Turn ? "Игроку 1" : "Игроку 2")}";
                        UpdateBoard(enemyCanvas, targetBoard, true);
                        UpdateStats();
                        await Task.Delay(1200);
                        isPlayer2Turn = !isPlayer2Turn;
                        UpdateStatusAndBoards();
                        return;
                    }
                    UpdateBoard(enemyCanvas, targetBoard, true);
                }
            }
            finally
            {
                enemyCanvas.IsEnabled = true;
            }
        }
        else
        {
            // Режим против компьютера
            if (!playerTurn) return;

            var (hit, sunk, gameOver) = computerBoard.Attack(x, y);

            if (hit)
            {
                playerHits++;
                SoundManager.PlayHit();

                if (sunk)
                {
                    SoundManager.PlaySunk();
                    statusText.Text = gameOver
                        ? "🎉 ПОБЕДА! Вы потопили весь флот противника!"
                        : "💥 Корабль потоплен! Продолжайте атаку!";

                    if (gameOver)
                    {
                        SoundManager.PlayWin();
                        playerTurn = false;
                    }
                }
                else
                {
                    statusText.Text = "🔥 ПОПАДАНИЕ! Атакуйте снова!";
                }

                UpdateStats();
                UpdateBoard(enemyCanvas, computerBoard, true);

                if (!gameOver)
                {
                    return;
                }
            }
            else if (computerBoard.Grid[x, y] == CellState.Miss)
            {
                playerMisses++;
                SoundManager.PlayMiss();
                statusText.Text = "💧 Промах! Ход переходит к противнику...";
                UpdateStats();
                UpdateBoard(enemyCanvas, computerBoard, true);
                playerTurn = false;

                await Task.Delay(800);
                if (botDifficulty == BotDifficulty.Easy)
                    await ComputerTurn();
                else
                    await ComputerTurnSmart();
            }
        }
    }

    private async Task ComputerTurn()
    {
        var possibleTargets = new List<(int x, int y)>();
        for (int x = 0; x < playerBoard.Size; x++)
        {
            for (int y = 0; y < playerBoard.Size; y++)
            {
                if (playerBoard.Grid[x, y] == CellState.Empty || playerBoard.Grid[x, y] == CellState.Ship)
                {
                    possibleTargets.Add((x, y));
                }
            }
        }

        bool continueTurn = true;

        while (continueTurn && !playerTurn && possibleTargets.Count > 0)
        {
            int randomIndex = random.Next(possibleTargets.Count);
            var (x, y) = possibleTargets[randomIndex];
            possibleTargets.RemoveAt(randomIndex);

            var (hit, sunk, gameOver) = playerBoard.Attack(x, y);

            if (hit)
            {
                computerHits++;
                SoundManager.PlayHit();

                if (sunk)
                {
                    SoundManager.PlaySunk();
                    statusText.Text = gameOver
                        ? "💀 ПОРАЖЕНИЕ! Противник уничтожил ваш флот!"
                        : "⚠️ Противник потопил ваш корабль!";

                    if (gameOver)
                    {
                        SoundManager.PlayLose();
                        continueTurn = false;
                    }
                }
                else
                {
                    statusText.Text = "💥 Противник попал в ваш корабль!";
                }

                UpdateStats();
                UpdateBoard(ownCanvas, playerBoard, false);
            }
            else
            {
                computerMisses++;
                SoundManager.PlayMiss();
                statusText.Text = "⚔️ Противник промахнулся! ВАШ ХОД!";
                playerTurn = true;
                continueTurn = false;

                UpdateStats();
                UpdateBoard(ownCanvas, playerBoard, false);
            }

            if (gameOver)
            {
                continueTurn = false;
                playerTurn = false;
            }

            if (continueTurn && !gameOver)
            {
                await Task.Delay(500);
            }
        }
    }

    private async Task ComputerTurnSmart()
    {
        var possibleTargets = GetSmartTargets(); // ← НОВЫЙ МЕТОД!

        bool continueTurn = true;
        while (continueTurn && !playerTurn && possibleTargets.Count > 0)
        {
            (int x, int y) target = GetNextSmartShot(possibleTargets); // ← УМНЫЙ ВЫБОР!
            possibleTargets.Remove((target.x, target.y));

            var (hit, sunk, gameOver) = playerBoard.Attack(target.x, target.y);

            if (hit)
            {
                lastHits.Add((target.x, target.y)); // ← Запоминаем попадание
                if (lastHits.Count > 5) lastHits.RemoveAt(0); // Храним последние 5

                computerHits++;
                SoundManager.PlayHit();

                if (sunk)
                {
                    SoundManager.PlaySunk();
                    lastHits.Clear(); // Сбрасываем при потоплении
                    statusText.Text = gameOver ? "☣️⚰️ ПОРАЖЕНИЕ!" : "Противник потопил корабль!";
                    if (gameOver)
                    {
                        SoundManager.PlayLose();
                        continueTurn = false;
                    }
                }
                else
                {
                    statusText.Text = "Противник попал!";
                }
            }
            else
            {
                computerMisses++;
                SoundManager.PlayMiss();
                statusText.Text = "⚔️ Противник промахнулся! ВАШ ХОД!";
                playerTurn = true;
                continueTurn = false;
            }

            UpdateStats();
            UpdateBoard(ownCanvas, playerBoard, false);

            if (continueTurn && !gameOver)
                await Task.Delay(500);
        }
    }

    private List<(int x, int y)> GetSmartTargets()
    {
        var targets = new List<(int x, int y)>();

        for (int x = 0; x < playerBoard.Size; x++)
            for (int y = 0; y < playerBoard.Size; y++)
            {
                if (playerBoard.Grid[x, y] == CellState.Empty || playerBoard.Grid[x, y] == CellState.Ship)
                    targets.Add((x, y));
            }

        return targets;
    }

    private (int x, int y) GetNextSmartShot(List<(int x, int y)> possibleTargets)
    {
        // === СЛОЖНЫЙ ИИ: стреляем рядом с попаданиями ===
        if (botDifficulty == BotDifficulty.Hard && lastHits.Count > 0)
        {
            if (lastHits.Count > 0)
            {
                var last = lastHits.Last();
                if (lastHits.Count > 1)
                {
                    var prev = lastHits[^2];
                    int dx = last.x - prev.x;
                    int dy = last.y - prev.y;
                    int nextX = last.x + dx;
                    int nextY = last.y + dy;

                    if (IsValidAndAvailable(nextX, nextY, possibleTargets))
                        return (nextX, nextY);
                }

                // 2. Пробуем противоположное направление (если было промах)
                if (lastHitDirection.HasValue)
                {
                    var (dx, dy) = lastHitDirection.Value;
                    int nextX = last.x + dx;
                    int nextY = last.y + dy;

                    if (IsValidAndAvailable(nextX, nextY, possibleTargets))
                        return (nextX, nextY);
                }

                // 3. Соседи последнего попадания
                var neighbors = GetNeighbors(last.x, last.y);
                foreach (var neighbor in neighbors)
                    if (possibleTargets.Contains(neighbor))
                        return neighbor;
            }
        }
        // === СРЕДНИЙ: приоритет соседям попаданий ===
        if (botDifficulty == BotDifficulty.Medium)
        {
            var allNeighbors = new List<(int x, int y)>();
            foreach (var hit in lastHits)
            {
                allNeighbors.AddRange(GetNeighbors(hit.x, hit.y));
            }

            // Убираем дубликаты
            var uniqueNeighbors = allNeighbors.Distinct().ToList();

            foreach (var neighbor in uniqueNeighbors)
            {
                if (possibleTargets.Contains(neighbor))
                    return neighbor;
            }
        }

        // === ЛЁГКИЙ: рандом ===
        return possibleTargets[random.Next(possibleTargets.Count)];
    }

    private bool IsValidAndAvailable(int x, int y, List<(int x, int y)> targets)
    {
        return x >= 0 && x < playerBoard.Size && y >= 0 && y < playerBoard.Size &&
               (playerBoard.Grid[x, y] == CellState.Empty || playerBoard.Grid[x, y] == CellState.Ship) &&
               targets.Contains((x, y));
    }

    private List<(int x, int y)> GetNeighbors(int x, int y)
    {
        var neighbors = new List<(int x, int y)>();
        int[][] directions = { [-1, 0], [1, 0], [0, -1], [0, 1] }; // 4 стороны

        foreach (var dir in directions)
        {
            int nx = x + dir[0];
            int ny = y + dir[1];
            if (nx >= 0 && nx < playerBoard.Size && ny  >= 0 && ny < playerBoard.Size)
                neighbors.Add((nx, ny));
        }

        return neighbors;
    }

    private void UpdateStats()
    {
        if (networkMode == NetworkGameMode.InGame)
        {
            // Сетевая игра: используем отдельную статистику
            playerStatsText.Text = $"🎯 Ваши выстрелы: {playerHits} попаданий, {playerMisses} промахов";
            opponentStatsText.Text = $"💣 Выстрелы {opponentName}: {opponentHits} попаданий, {opponentMisses} промахов";
        }
        else
        {
            if (currentMode == GameMode.VsPlayer)
            {
                int ownHits = isPlayer2Turn ? computerHits : playerHits;
                int ownMisses = isPlayer2Turn ? computerMisses : playerMisses;
                int enemyHits = isPlayer2Turn ? playerHits : computerHits;
                int enemyMisses = isPlayer2Turn ? playerMisses : computerMisses;
                playerStatsText.Text = $"🎯 Ваши выстрелы: {ownHits} попаданий, {ownMisses} промахов";
                computerStatsText.Text = $"💣 Выстрелы противника: {enemyHits} попаданий, {enemyMisses} промахов";
            }
            else // VsComputer
            {
                playerStatsText.Text = $"🎯 Ваши выстрелы: {playerHits} попаданий, {playerMisses} промахов";
                computerStatsText.Text = $"💣 Выстрелы противника: {computerHits} попаданий, {computerMisses} промахов";
            }
        }
    }

    private void UpdateStatusAndBoards()
    {
        Console.WriteLine($"[DEBUG] UpdateStatusAndBoards called: networkMode={networkMode}, playerTurn={playerTurn}");
        if (networkMode != NetworkGameMode.InGame)
        {
            // Локальная игра: обновляем статус
            if (currentMode == GameMode.VsPlayer)
            {
                statusText.Text = isPlayer2Turn
                    ? "⚔️ ВАШ ХОД, ИГРОК 2! Атакуйте поле противника"
                    : "⚔️ ВАШ ХОД, ИГРОК 1! Атакуйте поле противника";
            }
            else if (currentMode == GameMode.VsComputer)
            {
                statusText.Text = playerTurn ? "⚔️ ВАШ ХОД! Атакуйте поле противника" : "💀 Ход противника...";
            }
        }

        GameBoard ownBoard = (currentMode == GameMode.VsPlayer && isPlayer2Turn) ? computerBoard : playerBoard;
        GameBoard enemyBoard;
    
        if (networkMode == NetworkGameMode.InGame)
        {
            enemyBoard = opponentBoard;
        }
        else if (currentMode == GameMode.VsPlayer && networkMode == NetworkGameMode.None)
        {
            enemyBoard = isPlayer2Turn ? playerBoard : computerBoard;
        }
        else // VsComputer
        {
            enemyBoard = computerBoard;
        }

        string ownTitle = "🛡️ ВАШЕ ПОЛЕ";
        string enemyTitle = GetEnemyBoardTitle();

        UpdateHeaderText(ownCanvas.Parent, ownTitle);
        UpdateHeaderText(enemyCanvas.Parent, enemyTitle);

        UpdateBoard(ownCanvas, ownBoard, false);
        UpdateBoard(enemyCanvas, enemyBoard, true);

        UpdateStats();
        Console.WriteLine("[DEBUG] Boards updated");
    }
    
    // Вспомогательный метод для заголовка доски соперника
    private string GetEnemyBoardTitle()
    {
        if (networkMode == NetworkGameMode.InGame)
        {
            return $"🎯 ПОЛЕ {opponentName.ToUpper()}";
        }
        else if (currentMode == GameMode.VsPlayer)
        {
            return isPlayer2Turn ? "🎯 ПОЛЕ ИГРОКА 1" : "🎯 ПОЛЕ ИГРОКА 2";
        }
        else // GameMode.VsComputer
        {
            return "🎯 ПОЛЕ ПРОТИВНИКА";
        }
    }

    private void UpdateHeaderText(object parent, string text)
    {
        if (parent is StackPanel panel && panel.Children[0] is Border border && border.Child is TextBlock label)
            label.Text = text;
    }

    private void UpdateBoard(Canvas canvas, GameBoard board, bool isEnemy)
    {
        canvas.Children.Clear();

        int cellSize = 40;
        int padding = 10;

        for (int i = 0; i < board.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString(),
                Classes = { "Coordinate" }
            };
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString(),
                Classes = { "Coordinate" }
            };
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize / 2 - 7);
            canvas.Children.Add(numberText);
        }

        for (int i = 0; i < board.Size; i++)
        {
            for (int j = 0; j < board.Size; j++)
            {
                var cell = CreateGameCell(board, i, j, cellSize, isEnemy);
                Canvas.SetLeft(cell, padding + i * cellSize);
                Canvas.SetTop(cell, padding + j * cellSize);
                canvas.Children.Add(cell);
            }
        }
    }
}