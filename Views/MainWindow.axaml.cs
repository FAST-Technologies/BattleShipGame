// using Avalonia.Controls;
//
// namespace BattleShipGame2.Views;
//
// public partial class MainWindow : Window
// {
//     public MainWindow()
//     {
//         InitializeComponent();
//     }
// }
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using BattleShipGame2.Models;
using BattleShipGame2.Networking;
using BattleShipGame2.Logic;

namespace BattleShipGame2.Views;

/// <summary>
/// Главное окно игры «Морской бой».
/// Поддерживает три режима:
/// • против компьютера (с тремя уровнями сложности),
/// • локальная игра на двоих,
/// • сетевая игра через собственный сервер.
/// </summary>
public partial class MainWindow : Window
{
    
    #region Поля и свойства
    
    private Canvas _ownCanvas;
    private Canvas _enemyCanvas;

    private GameBoard? playerBoard;          /// <summary>Собственная игровая доска игрока.</summary>
    private GameBoard? computerBoard;        /// <summary>Доска компьютера (режим против ИИ).</summary>
    private GameBoard? opponentBoard;        /// <summary>Доска соперника в сетевой игре.</summary>

    private TextBlock? statusText;           /// <summary>Текст текущего статуса игры (чей ход, результат и т.п.).</summary>
    private TextBlock playerStatsText;      /// <summary>Статистика выстрелов игрока.</summary>
    private TextBlock computerStatsText;    /// <summary>Статистика выстрелов компьютера (локальный режим).</summary>
    private TextBlock opponentStatsText;    /// <summary>Статистика выстрелов соперника (сетевая игра).</summary>

    private GameMode currentMode = GameMode.Menu;           /// <summary>Текущий режим игры (меню, против ПК, вдвоём, онлайн).</summary>

    private bool playerTurn = true;         /// <summary>Флаг, чей сейчас ход в сетевой/локальной игре.</summary>
    private bool isPlayer2Turn = false;     /// <summary>Флаг хода второго игрока в локальном режиме «на двоих».</summary>

    private int playerHits = 0;             /// <summary>Количество попаданий игрока.</summary>
    private int playerMisses = 0;           /// <summary>Количество промахов игрока.</summary>
    private int computerHits = 0;           /// <summary>Количество попаданий компьютера.</summary>
    private int computerMisses = 0;         /// <summary>Количество промахов компьютера.</summary>
    private int opponentHits = 0;           /// <summary>Количество попаданий соперника (сетевая игра).</summary>
    private int opponentMisses = 0;         /// <summary>Количество промахов соперника (сетевая игра).</summary>

    private GameMode _lastGameMode = GameMode.VsComputer;   /// <summary>Последний выбранный режим (для кнопки «Новая игра»).</summary>

    // --------------------------------------------------------------------
    // Расстановка кораблей вручную
    // --------------------------------------------------------------------
    private List<int> shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
    /// <summary>Список размеров кораблей, которые нужно разместить (4-палубный, два 3-палубных и т.д.).</summary>

    private int currentShipIndex = 0;       /// <summary>Индекс текущего размещаемого корабля.</summary>
    private bool currentShipHorizontal = true; /// <summary>Ориентация текущего корабля (true — горизонтально).</summary>

    private GameBoard placingBoard;         /// <summary>Доска, на которой сейчас происходит расстановка (playerBoard или computerBoard).</summary>
    private bool placingPlayer1Ships = true; /// <summary>true — расставляет первый игрок, false — второй (локальный режим).</summary>
    
    // Боты
    private BotManager _botManager = new BotManager(); /// <summary>Менеджер ботов.</summary>
    private BotDifficulty botDifficulty = BotDifficulty.Easy; /// <summary>Сложность бота по умолчанию.</summary>
    
    // --------------------------------------------------------------------
    // Сетевые поля
    // --------------------------------------------------------------------
    
    private ChatManager? _chatManager; /// <summary>Инициализация чат-менеджера.</summary>
    private NetworkGameManager _networkManager; /// <summary>Инициализация сетевого менеджера.</summary>
    private NetworkClient networkClient = new NetworkClient(); /// <summary>Клиент для соединения с сервером.</summary>
    private bool _gameOver = false; /// <summary>Флаг окончания игры.</summary>
    private bool _isNetworkGameActive = false;
    
    // --------------------------------------------------------------------
    // UI-элементы игрового поля
    // --------------------------------------------------------------------
    private Canvas placementCanvas; /// <summary>Canvas для ручной расстановки кораблей.</summary>
    private Canvas ownCanvas;       /// <summary>Левое поле — всегда своё (с видимыми кораблями).</summary>
    private Canvas enemyCanvas;     /// <summary>Правое поле — поле противника.</summary>

    private bool _isProcessingNetworkAttack = false; /// <summary>Блокировка повторных атак пока ждём результат от сервера.</summary>

    private List<(string sender, string message, DateTime timestamp)> chatMessages = new();
    /// <summary>Список сообщений чата в сетевой игре.</summary>

    private TextBox chatInputBox;     /// <summary>Поле ввода сообщения в чате.</summary>
    private ScrollViewer chatScrollViewer; /// <summary>ScrollViewer для прокрутки чата.</summary>

    private Action _currentConfirmAction;
    private bool _isGameScreenVisible = false;
    private bool _isProcessingGameOver = false;
    private bool _isGameOverProcessing = false; // Добавить в поля класса
    private object _gameOverLock = new object(); // Добавить для синхронизации
    
    #endregion


    #region Конструктор и инициализация

    /// <summary>
    /// Инициализирует главное окно, задаёт заголовок, размеры, фон и запускает экран загрузки.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        InitializeEventHandlers();
        

        _networkManager = new NetworkGameManager(networkClient);
        SubscribeToNetworkEvents();
        
        ShowLoadingScreen();
    }

    private void InitializeEventHandlers()
    {
        // Главное меню
        VsComputerButton.Click += (s, e) => ShowDifficultyWindow();
        VsPlayerButton.Click += (s, e) => StartGame(GameMode.VsPlayer);
        VsOnlineButton.Click += (s, e) => ShowNetworkConnectWindow();
        
        // Расстановка
        RotateShipButton.Click += (s, e) => RotateCurrentShip();
        RandomPlacementButton.Click += (s, e) => PlaceShipsRandomly();
        StartGameButton.Click += (s, e) => FinishPlacement();
        
        // Игровой экран
        NewGameButton.Click += (s, e) => OnNewGameClick();
        ToMenuButton.Click += (s, e) => OnToMenuClick();
    }
    
    #endregion
    
    #region Network Event Handlers
    
    private void SubscribeToNetworkEvents()
    {
        _networkManager.StatusChanged += (status) => 
            Dispatcher.UIThread.Post(() => OnNetworkStatusChanged(status));
            
        _networkManager.PlayerTurnChanged += (isPlayerTurn) => 
            Dispatcher.UIThread.Post(() => OnPlayerTurnChanged(isPlayerTurn));
            
        _networkManager.GameStarted += (playerName, opponentName) => 
            Dispatcher.UIThread.Post(() => OnNetworkGameStarted(playerName, opponentName));
            
        _networkManager.GameOver += (winnerName, iWon) => 
            Dispatcher.UIThread.Post(() => OnNetworkGameOver(winnerName, iWon));
            
        _networkManager.OpponentLeft += (message) => 
            Dispatcher.UIThread.Post(() => OnOpponentLeft(message));
            
        _networkManager.OpponentDisconnected += (message) => 
            Dispatcher.UIThread.Post(() => OnOpponentDisconnected(message));
            
        _networkManager.ConnectionLost += (message) => 
            Dispatcher.UIThread.Post(() => OnConnectionLost(message));
        
        _networkManager.JoinedReceived += (message) => 
            Dispatcher.UIThread.Post(() => OnJoinedReceived(message));
            
        _networkManager.MatchFoundReceived += () => 
            Dispatcher.UIThread.Post(() => OnMatchFound());
            
        _networkManager.GameStartReceived += (playerTurn) => 
            Dispatcher.UIThread.Post(() => OnGameStartReceived(playerTurn));
            
        _networkManager.YourTurnReceived += () => 
            Dispatcher.UIThread.Post(() => OnYourTurn());
            
        _networkManager.YourTurnAgainReceived += () => 
            Dispatcher.UIThread.Post(() => OnYourTurnAgain());
            
        _networkManager.OpponentTurnReceived += () => 
            Dispatcher.UIThread.Post(() => OnOpponentTurn());
            
        _networkManager.AttackResultReceived += (x, y, hit, sunk, gameOver, isMyAttack, data) => 
            Dispatcher.UIThread.Post(() => OnAttackResultReceived(x, y, hit, sunk, gameOver, isMyAttack, data));
        
        _networkManager.GameOver += (winnerName, iWon) => 
        {
            Console.WriteLine($"[DEBUG] GameOver event received: winner={winnerName}, iWon={iWon}");
        
            // Защита от повторной обработки
            lock (_gameOverLock)
            {
                if (_isGameOverProcessing) 
                {
                    Console.WriteLine($"[DEBUG] GameOver already processing, skipping");
                    return;
                }
                _isGameOverProcessing = true;
            }
        
            Dispatcher.UIThread.Post(() => 
            {
                try
                {
                    OnNetworkGameOver(winnerName, iWon);
                }
                finally
                {
                    lock (_gameOverLock)
                    {
                        _isGameOverProcessing = false;
                    }
                }
            });
        };
    }
    
    private void InitializeNetworkGameBoards()
    {
        Console.WriteLine($"[DEBUG] Initializing network game boards...");
    
        // Получаем доски из NetworkManager
        playerBoard = _networkManager.PlayerBoard;
        opponentBoard = _networkManager.OpponentBoard;
    
        Console.WriteLine($"[DEBUG] playerBoard from manager: {playerBoard != null}");
        Console.WriteLine($"[DEBUG] opponentBoard from manager: {opponentBoard != null}");
    
        // Если доски все еще null, создаем новые
        #if DEBUG
        if (playerBoard == null)
        {
            playerBoard = new GameBoard();
            Console.WriteLine($"[DEBUG] Created new playerBoard");
        }
        if (opponentBoard == null)
        {
            opponentBoard = new GameBoard();
            Console.WriteLine($"[DEBUG] Created new opponentBoard");
        }
        #else
        playerBoard ??= new GameBoard();
        opponentBoard ??= new GameBoard();
        #endif
        // Убедимся, что NetworkManager знает об этих досках
        _networkManager?.PlayerBoard = playerBoard;
        _networkManager?.OpponentBoard = opponentBoard;
    
        Console.WriteLine($"[DEBUG] Boards initialized successfully");
    }
    
    private void OnNewGameClick()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            ShowConfirmDialog(
                "Начать новую онлайн-игру?\nТекущая игра будет завершена.",
                () => {
                    LeaveNetworkGameAsync();
                    ShowNetworkConnectWindow();
                }
            );
        else
            StartGame(currentMode);
    }

    private void OnToMenuClick()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            ShowConfirmDialog(
                "Вернуться в главное меню?\nТекущая игра будет завершена.",
                () => {
                    LeaveNetworkGameAsync();
                    ShowMainMenu();
                }
            );
        else
            ShowMainMenu();
    }
    
    private void OnNetworkStatusChanged(string status)
    {
        GameStatusText?.Text = status;
    }
    
    private void OnPlayerTurnChanged(bool isPlayerTurn)
    {
        playerTurn = isPlayerTurn;
        UpdateStatusAndBoards();
    }
    
    private void OnNetworkGameStarted(string playerName, string opponentName)
    {
        StartNetworkGame();
    }
    
    private async void OnNetworkGameOver(string winnerName, bool iWon)
    {
        // Защита от повторной обработки
        if (_isProcessingGameOver) 
        {
            Console.WriteLine($"[DEBUG] Already processing game over, skipping");
            return;
        }
    
        _isProcessingGameOver = true;
    
        try
        {
            Console.WriteLine($"[DEBUG] OnNetworkGameOver: winner={winnerName}, iWon={iWon}");
        
            // Даем время обработать последний ATTACK_RESULT
            await Task.Delay(300);
        
            await Dispatcher.UIThread.InvokeAsync(() => 
                ShowNetworkGameOverDialog(winnerName, iWon));
        }
        finally
        {
            _isProcessingGameOver = false;
        }
    }
    
    private async void OnOpponentLeft(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => 
            ShowOpponentLeftDialog(message));
    }
    
    private async void OnOpponentDisconnected(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => 
            ShowOpponentDisconnectedDialog(message));
    }
    
    private void OnConnectionLost(string message)
    {
        _isNetworkGameActive = false;
        _isGameScreenVisible = false;
    
        // Сбрасываем состояние сетевой игры
        _ = LeaveNetworkGameAsync(true);
    
        Dispatcher.UIThread.Post(() => 
        {
            GameStatusText?.Text = message;
            ShowMainMenu();
        });
    }
    
    private void OnJoinedReceived(string message)
    {
        GameStatusText?.Text = message;
    }
    
    private void OnMatchFound()
    {
        StartNetworkGame();
    }
    
    private void OnGameStartReceived(bool isPlayerTurn)
    {
        playerTurn = isPlayerTurn;
        ShowGameScreen();
    }
    
    private void OnYourTurn()
    {
        playerTurn = true;
        if (_isGameScreenVisible)
            UpdateStatusAndBoards();
    }

    private void OnYourTurnAgain()
    {
        playerTurn = true;
        if (_isGameScreenVisible)
            UpdateStatusAndBoards();
    }

    private void OnOpponentTurn()
    {
        playerTurn = false;
        if (_isGameScreenVisible)
            UpdateStatusAndBoards();
    }
    
    private void OnAttackResultReceived(int x, int y, bool hit, bool sunk, bool gameOver, bool isMyAttack, Dictionary<string, string> data)
    {
        HandleAttackResultMessage(x, y, hit, sunk, gameOver, isMyAttack, data);
    }
    
    #endregion
    
    #region Экран загрузки
    
    private void HideAllScreens()
    {
        LoadingScreen?.IsVisible = false;
        MainMenuScreen?.IsVisible = false;
        PlacementScreen?.IsVisible = false;
        GameScreen?.IsVisible = false;
        _isGameScreenVisible = false;
    }
    
    private async void ShowLoadingScreen()
    {
        HideAllScreens();
        LoadingScreen?.IsVisible = true;
        await SimulateLoadingAsync();
        ShowMainMenu();
    }

    private async Task SimulateLoadingAsync()
    {
        var loadingSteps = new[]
        {
            ("Загрузка ресурсов...", 20),
            ("Инициализация графики...", 40),
            ("Подготовка игровых досок...", 60),
            ("Загрузка звуков...", 80),
            ("Финализация...", 100)
        };

        foreach (var (status, progress) in loadingSteps)
        {
            LoadingStatusText.Text = status;
        
            var targetWidth = (400.0 - 4) * progress / 100;
            var currentWidth = LoadingProgressBar.Width;
            var steps = 20;
            var increment = (targetWidth - currentWidth) / steps;

            for (int i = 0; i < steps; i++)
            {
                LoadingProgressBar.Width = currentWidth + increment * (i + 1);
                await Task.Delay(30);
            }

            await Task.Delay(200);
        }

        LoadingStatusText.Text = "Готово! ✔";
        await Task.Delay(300);
    }
    
    #endregion
    
    #region Сетевое взаимодействие
    
    private async Task<(bool success, string errorMessage)> ConnectToServer(string hostname, int port, string playerName)
    {
        return await _networkManager.ConnectToServer(hostname, port, playerName);
    }
    
    private void ResetPlacementState()
    {
        shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
        currentShipIndex = 0;
        currentShipHorizontal = true;
        placingPlayer1Ships = true;
    
        // Сброс состояния доски
        playerBoard?.Clear();
        computerBoard?.Clear();
        opponentBoard?.Clear();
    }
    
    private void StartNetworkGame()
    {
        ResetPlacementState();
        playerHits = 0;
        playerMisses = 0;
        opponentHits = 0;
        opponentMisses = 0;
        _gameOver = false;
        _isProcessingNetworkAttack = false;
        _isNetworkGameActive = true;
        currentMode = GameMode.VsPlayer;
    
        // ВАЖНО: Инициализируем доски!
        InitializeNetworkGameBoards();
    
        placingBoard = playerBoard;
        placingPlayer1Ships = true;
        currentShipIndex = 0;
        currentShipHorizontal = true;
        playerTurn = false;
        isPlayer2Turn = false;
    
        _chatManager = new ChatManager(networkClient, _networkManager.PlayerName);
        Dispatcher.UIThread.Post(() => 
        {
            ShowShipPlacementScreen();
            PlacementStatusText?.Text = $"Найден соперник: {_networkManager.OpponentName}! Начинаем расстановку...";
        });
    }
    
    private async Task OnNetworkGameCellClickAsync(int x, int y)
    {
        Console.WriteLine($"[DEBUG] OnNetworkGameCellClickAsync: x={x}, y={y}, playerTurn={playerTurn}");
    
        if (!playerTurn || _isProcessingNetworkAttack)
        {
            Console.WriteLine($"[DEBUG] Attack rejected");
            return;
        }

        var cellState = opponentBoard.Grid[x, y];
        if (cellState != CellState.Empty && cellState != CellState.Ship)
        {
            Console.WriteLine($"[DEBUG] Cell already attacked");
            return;
        }

        _isProcessingNetworkAttack = true;
        await _networkManager.SendAttackAsync(x, y);
        _isProcessingNetworkAttack = false;
    }
    
    private async Task LeaveNetworkGameAsync(bool clearBoards = true)
    {
        Console.WriteLine($"[DEBUG] Leaving network game (clearBoards={clearBoards})...");

        _isNetworkGameActive = false;
        _isGameScreenVisible = false;
        _gameOver = true;

        if (_networkManager != null)
            await _networkManager.LeaveGameAsync();
    
        // ВАЖНО: Очищаем доски только если это явный выход,
        // а не завершение игры (когда нужно показать финальное состояние)
        if (clearBoards)
        {
            playerBoard = null;
            opponentBoard = null;
            Console.WriteLine($"[DEBUG] Boards cleared");
        }
        else
        {
            Console.WriteLine($"[DEBUG] Boards preserved for final display");
        }

        Console.WriteLine($"[DEBUG] Network game left successfully");
    }
    
    #endregion

    #region Главное меню и UI
    
    private void ShowMainMenu()
    {
        Console.WriteLine($"[DEBUG] Showing main menu");
    
        // Отписываемся от событий ChatManager
        _chatManager = null;
    
        // Сбрасываем флаги
        _isNetworkGameActive = false;
        _isGameScreenVisible = false;
    
        // Сбрасываем состояние игры - ТОЛЬКО ЗДЕСЬ!
        playerBoard = null;
        computerBoard = null;
        opponentBoard = null;
        playerHits = 0;
        playerMisses = 0;
        computerHits = 0;
        computerMisses = 0;
        opponentHits = 0;
        opponentMisses = 0;
        _gameOver = false;
    
        // Сбрасываем состояние расстановки
        shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
        currentShipIndex = 0;
        currentShipHorizontal = true;
        placingPlayer1Ships = true;
    
        // Сетевое соединение
        if (_networkManager?.NetworkMode == NetworkGameMode.InGame)
        {
            if (networkClient?.IsConnected == true)
                // Асинхронно выходим из игры
                _ = LeaveNetworkGameAsync(true);
        }
        else if (networkClient?.IsConnected == true)
            networkClient.Disconnect();
    
        currentMode = GameMode.Menu;
        HideAllScreens();
        
        MainMenuScreen?.IsVisible = true;

        Console.WriteLine($"[DEBUG] Main menu shown");
    }
    
    #endregion
    
    #region Окно выбора сложности

    private async void ShowDifficultyWindow()
    {
        var difficultyWindow = new DifficultyWindow();
        await difficultyWindow.ShowDialog(this);
    
        if (difficultyWindow.SelectedDifficulty.HasValue)
        {
            botDifficulty = difficultyWindow.SelectedDifficulty.Value;
            _botManager.SetDifficulty(botDifficulty);
            StartGame(GameMode.VsComputer);
        }
    }
    
    #endregion
    
    #region Сетевое подключение
    
    private async void ShowNetworkConnectWindow()
    {
        var connectWindow = new NetworkConnectWindow();
        await connectWindow.ShowDialog(this);
    
        if (connectWindow.Success)
        {
            var (connectSuccess, errorMessage) = await ConnectToServer(
                connectWindow.Hostname, 
                connectWindow.Port, 
                connectWindow.PlayerName);
        
            if (connectSuccess)
                GameStatusText?.Text = $"Подключение к серверу... Ищу соперника...";
            else
            {
                var errorWindow = new OpponentDisconnectWindow();
                errorWindow.Message = errorMessage;
                errorWindow.Title = "Ошибка подключения";
                await errorWindow.ShowDialog(this);
            }
        }
    }
    
    #endregion

    #region Игровой процесс - Основной цикл

    private void StartGame(GameMode mode)
    {
        _lastGameMode = mode;
        if (_networkManager.NetworkMode != NetworkGameMode.None) return;
        
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
        _gameOver = false;
        if (mode == GameMode.VsComputer)
        {
            _botManager.SetDifficulty(botDifficulty);
            _botManager.ResetAll();
        }
        ShowShipPlacementScreen();
    }
    
    #endregion
    
    #region Расстановка кораблей
    
    private void UpdatePlacementInstructions()
    {
        if (currentShipIndex < shipsToPlace.Count)
        {
            PlacementInstructionText.Text = 
                $"Размещаем корабль размером {shipsToPlace[currentShipIndex]} клеток\nПробел - повернуть, ЛКМ - разместить";
        }
        else
        {
            PlacementInstructionText.Text = "Все корабли размещены!";
        }
    }

    private void RenderPlacementCanvas()
    {
        if (PlacementCanvas == null) return;
        
        PlacementCanvas.Children.Clear();

        int cellSize = 40;
        int padding = 10;

        // Координаты
        for (int i = 0; i < placingBoard.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString()
            };
            letterText.Classes.Add("Coordinate");
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            PlacementCanvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString()
            };
            numberText.Classes.Add("Coordinate");
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize / 2 - 7);
            PlacementCanvas.Children.Add(numberText);
        }

        // Клетки
        for (int i = 0; i < placingBoard.Size; i++)
        {
            for (int j = 0; j < placingBoard.Size; j++)
            {
                var cell = CreatePlacementCell(i, j, cellSize);
                Canvas.SetLeft(cell, padding + i * cellSize);
                Canvas.SetTop(cell, padding + j * cellSize);
                PlacementCanvas.Children.Add(cell);
            }
        }
    }
    
    private void ShowShipPlacementScreen()
    {
        HideAllScreens();
        PlacementScreen?.IsVisible = true;
        
        string playerName = "Игрок";
        if (currentMode == GameMode.VsPlayer && _networkManager.NetworkMode == NetworkGameMode.None)
            playerName = placingPlayer1Ships ? "Игрок 1" : "Игрок 2";
        else if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            playerName = "Вы";

        PlacementStatusText?.Text = $"🚢 {playerName}: Расставьте корабли";
            
        UpdatePlacementInstructions();
        RenderPlacementCanvas();
        
        KeyDown += OnPlacementKeyDown;
    }

    private Control CreatePlacementCell(int x, int y, int cellSize)
    {
        var border = new Border
        {
            Width = cellSize - 2,
            Height = cellSize - 2
        };
        border.Classes.Add("PlacementCell");

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
                var border = FindPlacementCellBorder(px, py);
                if (border != null && placingBoard.Grid[px, py] != CellState.Ship)
                {
                    border.Classes.Remove("CanPlace");
                    border.Classes.Remove("CannotPlace");
                    border.Classes.Remove("Empty");
                    if (highlight)
                        border.Classes.Add(canPlace ? "CanPlace" : "CannotPlace");
                    else
                        border.Classes.Add("Empty");
                }
            }
        }
    }

    private Border? FindPlacementCellBorder(int x, int y)
    {
        if (PlacementCanvas == null) return null;
        
        int cellSize = 40;
        int padding = 10;

        foreach (var child in PlacementCanvas.Children)
            if (child is Border border)
            {
                double left = Canvas.GetLeft(border);
                double top = Canvas.GetTop(border);

                if (Math.Abs(left - (padding + x * cellSize)) < 1 &&
                    Math.Abs(top - (padding + y * cellSize)) < 1)
                    return border;
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
            RenderPlacementCanvas();
            UpdatePlacementInstructions();

            if (currentShipIndex >= shipsToPlace.Count)
            {
                PlacementStatusText?.Text = "✅ Все корабли размещены! Нажмите 'Начать игру'";
                StartGameButton?.IsEnabled = true;
            }
        }
    }

    private void PlaceShipsRandomly()
    {
        placingBoard.Clear();
        placingBoard.PlaceShipsRandomly();
        currentShipIndex = shipsToPlace.Count;
        RenderPlacementCanvas();
        UpdatePlacementInstructions();
        PlacementStatusText?.Text = "✅ Все корабли размещены! Нажмите 'Начать игру'";
        EnableStartButton();
    }
    
    private void EnableStartButton()
    {
        StartGameButton?.IsEnabled = true;
    }

    private void OnPlacementKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) RotateCurrentShip();
    }

    private void RotateCurrentShip()
    {
        currentShipHorizontal = !currentShipHorizontal;
    }

    private async void FinishPlacement()
    {
        KeyDown -= OnPlacementKeyDown;

        if (currentMode == GameMode.VsPlayer && _networkManager.NetworkMode == NetworkGameMode.None && placingPlayer1Ships)
        {
            placingPlayer1Ships = false;
            placingBoard = computerBoard;
            currentShipIndex = 0;
            currentShipHorizontal = true;
            ShowShipPlacementScreen();
        }
        else if (currentMode == GameMode.VsComputer)
        {
            computerBoard.PlaceShipsRandomly();
            ShowGameScreen();
        }
        else if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            await _networkManager.SendShipPlacementAsync(placingBoard);
        
            GameStatusText?.Text = "Корабли расставлены! Ждем соперника...";
            return;
        }
        else
            ShowGameScreen();
        
    }
    
    #endregion
    
    #region Игровой процесс - основной экран
    
    private void ShowGameScreen()
    {
        HideAllScreens();
        if (GameScreen != null) 
        {
            GameScreen.IsVisible = true;
            _isGameScreenVisible = true;
        }
    
        isPlayer2Turn = false;
    
        // ВАЖНО: Инициализируем доски если они null
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            #if DEBUG
            if (playerBoard == null)
            {
                playerBoard = _networkManager.PlayerBoard ?? new GameBoard();
                Console.WriteLine($"[DEBUG] Initialized playerBoard in ShowGameScreen");
            }
            if (opponentBoard == null)
            {
                opponentBoard = _networkManager.OpponentBoard ?? new GameBoard();
                Console.WriteLine($"[DEBUG] Initialized opponentBoard in ShowGameScreen");
            }
            #else
            playerBoard ??= _networkManager.PlayerBoard ?? new GameBoard();
            opponentBoard ??= _networkManager.OpponentBoard ?? new GameBoard();
            #endif
        }
    
        if (_networkManager.NetworkMode == NetworkGameMode.InGame && _chatManager != null)
        {
            _chatManager = new ChatManager(networkClient, _networkManager.PlayerName);
            _networkManager.SetChatManager(_chatManager);
            var chatControl = _chatManager.CreateChatControl();
            ChatContainer?.Content = chatControl;
        }
        else
            ChatContainer?.Content = null;
    
        UpdateStatusAndBoards();
    }
    
    #endregion
    
    #region Обработка кликов по ячейкам
    
    private async void OnGameCellClick(int x, int y)
    {
        if (_networkManager.NetworkMode != NetworkGameMode.None) return;
        
        if (currentMode == GameMode.VsPlayer)
        {
            if (!playerTurn) return;
            
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
                        
                        GameStatusText?.Text = gameOver
                            ? $"🎉🏆️ ПОБЕДА! {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} потопил весь флот!"
                            : $"💥 {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} потопил корабль!";

                        if (gameOver)
                        {
                            if (isPlayer2Turn)
                                SoundManager.PlayLose();
                            else
                                SoundManager.PlayWin();
                            playerTurn = false;
                            _gameOver = true;
                
                            Dispatcher.UIThread.Post(() => 
                            {
                                ShowGameOverDialog(true, "Вы");
                            }, DispatcherPriority.Background);
                
                            UpdateStats();
                            UpdateBoards();
                            return;
                        }
                    }
                    else
                        GameStatusText?.Text = $"🔥 {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} попал! Стреляет снова!";
                    
                    UpdateStats();
                    UpdateBoards();
                    await Task.Delay(500);
                    return;
                }
                else if (targetBoard.Grid[x, y] == CellState.Miss)
                {
                    (isPlayer2Turn ? ref computerMisses : ref playerMisses)++;

                    SoundManager.PlayMiss();


                    GameStatusText?.Text = $"💧 {(isPlayer2Turn ? "Игрок 2" : "Игрок 1")} промахнулся! Ход переходит к {(isPlayer2Turn ? "Игроку 1" : "Игроку 2")}";
                    
                    UpdateStats();
                    UpdateBoards();
                    await Task.Delay(1200);
                    isPlayer2Turn = !isPlayer2Turn;
                    UpdateStatusAndBoards();
                    return;
                }
                
                UpdateBoards();
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
                    
                    GameStatusText?.Text = gameOver
                        ? "🎉 ПОБЕДА! Вы потопили весь флот противника!"
                        : "💥 Корабль потоплен! Продолжайте атаку!";


                    if (gameOver)
                    {
                        SoundManager.PlayWin();
                        playerTurn = false;
                        ShowGameOverDialog(true, "Вы");
                    }
                }
                else
                    GameStatusText?.Text = "🔥 ПОПАДАНИЕ! Атакуйте снова!";

                UpdateStats();
                UpdateBoards();

                if (!gameOver) return;
            }
            else if (computerBoard.Grid[x, y] == CellState.Miss)
            {
                playerMisses++;
                SoundManager.PlayMiss();
                
                GameStatusText?.Text = "💧 Промах! Ход переходит к противнику...";
                
                UpdateStats();
                UpdateBoards();
                
                playerTurn = false;

                await Task.Delay(800);
                if (botDifficulty == BotDifficulty.Easy)
                    await ComputerTurn();
                else
                    await ComputerTurnSmart();
            }
        }
    }
    
   private void UpdateBoards()
{
    // Если игровой экран не виден, не обновляем доски
    if (!_isGameScreenVisible) 
    {
        Console.WriteLine("[DEBUG] Game screen not visible, skipping UpdateBoards");
        return;
    }
    
    var ownCanvas = OwnCanvas;
    var enemyCanvas = EnemyCanvas;
    
    // Если Canvas не найдены, выходим
    if (ownCanvas == null || enemyCanvas == null)
    {
        Console.WriteLine("[WARNING] Canvas not found in UpdateBoards");
        return;
    }
    
    GameBoard? ownBoard = null;
    GameBoard? enemyBoard = null;
    
    try
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            ownBoard = playerBoard;
            enemyBoard = opponentBoard;
            
            Console.WriteLine($"[DEBUG] UpdateBoards - Network game mode detected");
            Console.WriteLine($"[DEBUG] playerBoard: {playerBoard != null}, opponentBoard: {opponentBoard != null}");
            Console.WriteLine($"[DEBUG] Game over flag: {_gameOver}, isGameOverProcessing: {_isGameOverProcessing}");
            
            // ВАЖНО: При завершении игры показываем все клетки
            if (_gameOver && !_isGameOverProcessing)
            {
                Console.WriteLine($"[DEBUG] Final board state - showing all cells");
                // Логируем состояние доски
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        if (enemyBoard != null && enemyBoard.Grid[i, j] == CellState.Sunk)
                            Console.WriteLine($"[DEBUG] Cell ({i},{j}) is Sunk");
                    }
                }
            }
        }
        else if (currentMode == GameMode.VsPlayer)
        {
            ownBoard = isPlayer2Turn ? computerBoard : playerBoard;
            enemyBoard = isPlayer2Turn ? playerBoard : computerBoard;
        }
        else // GameMode.VsComputer
        {
            ownBoard = playerBoard;
            enemyBoard = computerBoard;
        }
        
        // Проверяем что доски не null
        if (ownBoard == null)
        {
            Console.WriteLine($"[ERROR] Own board is still null!");
            return;
        }
        
        if (enemyBoard == null)
        {
            Console.WriteLine($"[ERROR] Enemy board is still null!");
            return;
        }
        
        // ВАЖНО: Проверяем состояние клеток перед отрисовкой
        if (_gameOver && !_isProcessingGameOver)
        {
            Console.WriteLine($"[DEBUG] Final board state before drawing:");
            Console.WriteLine($"[DEBUG] Own board size: {ownBoard.Size}, Enemy board size: {enemyBoard.Size}");
        }
        
        UpdateBoard(ownCanvas, ownBoard, false);
        UpdateBoard(enemyCanvas, enemyBoard, true);
        
        Console.WriteLine($"[DEBUG] UpdateBoards completed successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Exception in UpdateBoards: {ex.Message}");
        Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
    }
}
   
   private async Task ForceRedrawAfterGameOver(bool isMyAttack)
   {
       Console.WriteLine($"[DEBUG] ForceRedrawAfterGameOver called, isMyAttack={isMyAttack}");
    
       // Обновляем UI несколько раз с задержками
       for (int i = 0; i < 5; i++) // Увеличиваем количество обновлений
       {
           if (_isGameScreenVisible)
           {
               await Dispatcher.UIThread.InvokeAsync(() => 
               {
                   UpdateBoards();
                   UpdateStats();
                
                   // Принудительная перерисовка
                   var ownCanvas = OwnCanvas;
                   var enemyCanvas = EnemyCanvas;
                
                   if (ownCanvas != null)
                   {
                       ownCanvas.InvalidateVisual();
                       ownCanvas.InvalidateMeasure();
                       ownCanvas.InvalidateArrange();
                   }
                
                   if (enemyCanvas != null)
                   {
                       enemyCanvas.InvalidateVisual();
                       enemyCanvas.InvalidateMeasure();
                       enemyCanvas.InvalidateArrange();
                   }
               }, DispatcherPriority.Render);
            
               await Task.Delay(50); // Уменьшаем задержку между обновлениями
           }
       }
    
       Console.WriteLine($"[DEBUG] ForceRedrawAfterGameOver completed");
   }
    
    #endregion
    
    #region Логика ботов
    
    private async Task ComputerTurn()
    {
        bool continueTurn = true;

        while (continueTurn && !playerTurn && !_gameOver)
        {
            var result = await _botManager.MakeSimpleTurn(
                playerBoard,
                HandleBotAttackResult
            );
            
            continueTurn = result.ContinueTurn && !result.GameOver;
            _gameOver = result.GameOver;
            
            if (continueTurn && !_gameOver)
            {
                await Task.Delay(500);
            }
            
            if (!continueTurn && !_gameOver)
            {
                playerTurn = true;
                GameStatusText?.Text = "⚔️ ВАШ ХОД! Атакуйте поле противника!";
                UpdateStatusAndBoards();
            }
            if (_gameOver)
            {
                playerTurn = false;
                continueTurn = false;
            }
        }
    }

    private async Task ComputerTurnSmart()
    {
        bool continueTurn = true;

        while (continueTurn && !playerTurn && !_gameOver)
        {
            var result = await _botManager.MakeSmartTurn(
                playerBoard,
                HandleBotAttackResult
            );
            
            continueTurn = result.ContinueTurn && !result.GameOver;
            _gameOver = result.GameOver;
            
            if (continueTurn && !_gameOver)
                await Task.Delay(500);
            
            if (!continueTurn && !_gameOver)
            {
                playerTurn = true;
                GameStatusText?.Text = "⚔️ ВАШ ХОД! Атакуйте поле противника!";
                UpdateStatusAndBoards();
            }
            if (_gameOver)
            {
                playerTurn = false;
                continueTurn = false;
            }
        }
    }

    private void HandleBotAttackResult(int x, int y, bool hit, bool sunk, bool gameOver)
    {
        _gameOver = gameOver;
    
        if (hit)
        {
            computerHits++;
            SoundManager.PlayHit();

            if (sunk)
            {
                SoundManager.PlaySunk();
            
                GameStatusText?.Text = gameOver
                    ? "💀 ПОРАЖЕНИЕ! Противник уничтожил ваш флот!"
                    : "⚠️ Противник потопил ваш корабль!";

                if (gameOver)
                {
                    SoundManager.PlayLose();
                    playerTurn = false;
                    _gameOver = true;
                    Dispatcher.UIThread.Post(() => 
                    {
                        ShowGameOverDialog(false, "Противник");
                    }, DispatcherPriority.Background);
                }
            }
            else
                GameStatusText?.Text = "💥 Противник попал в ваш корабль!";
        }
        else
        {
            computerMisses++;
            SoundManager.PlayMiss();
        
            GameStatusText?.Text = "⚔️ Противник промахнулся! ВАШ ХОД!";
        }

        UpdateStats();
        UpdateBoards();
    }
    
    #endregion
    
    #region Диалоговые окна
    
    private async void ShowConfirmDialog(string message, Action onConfirm)
    {
        var confirmWindow = new ConfirmDialogWindow();
        confirmWindow.Message = message;
    
        var result = await confirmWindow.ShowDialog<bool?>(this);
    
        if (result.HasValue && result.Value)
            onConfirm?.Invoke();
    }
    
    private async void ShowGameOverDialog(bool isWin, string winnerName)
    {
        var gameOverWindow = new GameOverWindow();
        gameOverWindow.IsWin = isWin;
        gameOverWindow.WinnerName = winnerName;
    
        await gameOverWindow.ShowDialog(this);
    
        if (gameOverWindow.Result.HasValue)
            if (gameOverWindow.Result.Value == GameOverResult.NewGame)
                StartGame(currentMode);
            else if (gameOverWindow.Result.Value == GameOverResult.MainMenu)
                ShowMainMenu();
    }
    
    private async Task ShowNetworkGameOverDialog(string winnerName, bool iWon)
{
    Console.WriteLine($"[DEBUG] ShowNetworkGameOverDialog: winner={winnerName}, iWon={iWon}");
    
    // Дополнительная проверка
    if (_isGameOverProcessing && _gameOver)
    {
        Console.WriteLine($"[DEBUG] Dialog already showing or game over processed, skipping");
        return;
    }

    if (GameStatusText != null) 
    {
        GameStatusText.Text = iWon 
            ? "🎉 ПОЗДРАВЛЯЕМ! Вы победили!" 
            : $"💀 ПОРАЖЕНИЕ! Победил {winnerName}";
    }

    // Устанавливаем флаг завершения игры
    _isGameOverProcessing = true;
    _gameOver = true;
    playerTurn = false;

    // ОБНОВЛЕНИЕ: Еще раз обновляем доски перед показом диалога
    UpdateBoards();
    UpdateStats();
    
    // Принудительная перерисовка
    var ownCanvas = OwnCanvas;
    var enemyCanvas = EnemyCanvas;
    
    ownCanvas?.InvalidateVisual();
    enemyCanvas?.InvalidateVisual();
    
    await Task.Delay(100); // Даем время для отрисовки

    var gameOverWindow = new NetworkGameOverWindow();
    gameOverWindow.IsWin = iWon;
    gameOverWindow.WinnerName = winnerName;

    // Блокируем ввод в главное окно
    this.IsEnabled = false;
    
    try
    {
        var result = await gameOverWindow.ShowDialog<NetworkGameOverResult?>(this);
    
        if (result.HasValue)
        {
            if (result.Value == NetworkGameOverResult.NewOnlineGame)
            {
                await LeaveNetworkGameAsync(true); // Только теперь очищаем доски
                ShowNetworkConnectWindow();
            }
            else if (result.Value == NetworkGameOverResult.MainMenu)
            {
                await LeaveNetworkGameAsync(true); // Только теперь очищаем доски
                ShowMainMenu();
            }
        }
        else
        {
            // Если пользователь просто закрыл окно
            await LeaveNetworkGameAsync(true); // Только теперь очищаем доски
            ShowMainMenu();
        }
    }
    finally
    {
        this.IsEnabled = true;
        _isGameOverProcessing = false;
    }
}

    
    private async void ShowOpponentLeftDialog(string message)
    {
        var opponentWindow = new OpponentDisconnectWindow();
        opponentWindow.Message = message;
        opponentWindow.Title = "Соперник покинул игру";
    
        var result = await opponentWindow.ShowDialog<bool?>(this);
    
        if (result.HasValue && result.Value)
        {
            await LeaveNetworkGameAsync(true);
            ShowMainMenu();
        }
    }
    
    private async void ShowOpponentDisconnectedDialog(string message)
    {
        var opponentWindow = new OpponentDisconnectWindow();
        opponentWindow.Message = message;
        opponentWindow.Title = "Соединение потеряно";
    
        var result = await opponentWindow.ShowDialog<bool?>(this);
    
        if (result.HasValue && result.Value)
        {
            await LeaveNetworkGameAsync(true);
            ShowMainMenu();
        }
    }
    
    #endregion
    
    #region Обработка сетевых сообщений

    private async void HandleAttackResultMessage(int x, int y, bool hit, bool sunk, bool gameOver, bool isMyAttack, Dictionary<string, string> data)
{
    Console.WriteLine($"[DEBUG] ATTACK_RESULT: ({x},{y}), hit={hit}, sunk={sunk}, gameOver={gameOver}, isMyAttack={isMyAttack}");

    // Защита от повторной обработки при завершении игры
    if (_gameOver && _isGameOverProcessing)
    {
        Console.WriteLine($"[DEBUG] Game already over or processing, ignoring attack result");
        return;
    }
    
    // ВАЖНО: Гарантируем, что доски инициализированы
    if (_networkManager.NetworkMode == NetworkGameMode.InGame)
    {
        if (playerBoard == null || opponentBoard == null)
        {
            Console.WriteLine($"[WARNING] Boards are null, initializing...");
            InitializeNetworkGameBoards();
        }
    }

    if (!_isGameScreenVisible)
    {
        Console.WriteLine($"[DEBUG] Game screen not visible, ignoring attack result");
        return;
    }

    // Получаем правильную доску
    GameBoard targetBoard = isMyAttack ? opponentBoard : playerBoard;

    if (targetBoard == null)
    {
        Console.WriteLine($"[ERROR] Target board is null in HandleAttackResultMessage");
        return;
    }

    // ОБНОВЛЕНИЕ: Всегда помечаем центральную клетку
    if (hit)
    {
        targetBoard.Grid[x, y] = sunk ? CellState.Sunk : CellState.Hit;
        
        if (isMyAttack) playerHits++;
        else opponentHits++;
        
        SoundManager.PlayHit();
        
        if (sunk)
        {
            SoundManager.PlaySunk();
            
            // ОБНОВЛЕНИЕ: Для потопленных кораблей ВСЕГДА обновляем все клетки
            if (data.ContainsKey(NetworkProtocol.Keys.SunkShipPositions))
            {
                var positions = data[NetworkProtocol.Keys.SunkShipPositions].Split(',');
                Console.WriteLine($"[DEBUG] Sunk ship positions: {string.Join(", ", positions)}");
                
                foreach (var pos in positions)
                {
                    var coords = pos.Split(':');
                    if (coords.Length == 2 && 
                        int.TryParse(coords[0], out int sx) && 
                        int.TryParse(coords[1], out int sy))
                    {
                        if (sx >= 0 && sx < targetBoard.Size && sy >= 0 && sy < targetBoard.Size)
                        {
                            // ВАЖНО: Помечаем ВСЕ клетки корабля как Sunk
                            targetBoard.Grid[sx, sy] = CellState.Sunk;
                            Console.WriteLine($"[DEBUG] Marking cell ({sx},{sy}) as Sunk");
                        }
                    }
                }
            }
            
            // Добавляем заблокированные клетки
            if (data.ContainsKey(NetworkProtocol.Keys.BlockedCells))
            {
                var blockedCells = data[NetworkProtocol.Keys.BlockedCells].Split(',');
                Console.WriteLine($"[DEBUG] Blocked cells: {string.Join(", ", blockedCells)}");
                
                foreach (var cell in blockedCells)
                {
                    var coords = cell.Split(':');
                    if (coords.Length == 2 && 
                        int.TryParse(coords[0], out int bx) && 
                        int.TryParse(coords[1], out int by))
                        if (bx >= 0 && bx < targetBoard.Size && by >= 0 && by < targetBoard.Size)
                            // Только пустые клетки помечаем как Blocked
                            if (targetBoard.Grid[bx, by] == CellState.Empty)
                            {
                                targetBoard.Grid[bx, by] = CellState.Blocked;
                                Console.WriteLine($"[DEBUG] Blocking cell ({bx},{by})");
                            }
                }
            }
        }
    }
    else
    {
        targetBoard.Grid[x, y] = CellState.Miss;
        if (isMyAttack) playerMisses++;
        else opponentMisses++;
        SoundManager.PlayMiss();
    }
    
    // Обновление статуса
    UpdateGameStatus(isMyAttack, hit, sunk, gameOver);
    
    // Обновление UI - СНАЧАЛА обновляем UI
    if (_isGameScreenVisible)
    {
        UpdateStats();
        UpdateBoards();
    }
    
    if (gameOver)
    {
        playerTurn = false;
        _gameOver = true;
        
        if (isMyAttack)
            SoundManager.PlayWin();
        else
            SoundManager.PlayLose();
        
        Console.WriteLine($"[DEBUG] Game over! Winner: {(isMyAttack ? "You" : _networkManager.OpponentName)}");

        // ОБНОВЛЕНИЕ: Обновляем UI еще раз чтобы показать все потопленные корабли
        if (_isGameScreenVisible)
        {
            // Принудительное обновление
            await Task.Delay(100); // Даем время для отрисовки предыдущих изменений
            UpdateBoards();
            await Task.Delay(100); // Еще немного для гарантии
            UpdateBoards();
        }

        // СНАЧАЛА принудительно перерисовываем несколько раз
        await ForceRedrawAfterGameOver(isMyAttack);
        
        // ПОТОМ показываем диалог с задержкой
        await Task.Delay(800); // Уменьшаем задержку
        
        await Dispatcher.UIThread.InvokeAsync(async () => 
        {
            if (_isGameScreenVisible)
            {
                await ShowNetworkGameOverDialog(
                    isMyAttack ? _networkManager.PlayerName : _networkManager.OpponentName, 
                    isMyAttack
                );
            }
        });
    }
}


    private void UpdateGameStatus(bool isMyAttack, bool hit, bool sunk, bool gameOver)
    {
        if (GameStatusText == null) return;
        
        if (gameOver)
            GameStatusText.Text = isMyAttack ? "🎉 ПОБЕДА!" : "💀 ПОРАЖЕНИЕ!";
        else if (sunk)
            GameStatusText.Text = isMyAttack 
                ? "💥 Корабль потоплен! Стреляйте снова!" 
                : "⚠️ Противник потопил ваш корабль!";
        else if (hit)
            GameStatusText.Text = isMyAttack 
                ? "🔥 ПОПАДАНИЕ! Стреляйте снова!" 
                : "💥 Противник попал в ваш корабль!";
        else
            GameStatusText.Text = isMyAttack 
                ? "💧 Промах! Ход переходит к сопернику..." 
                : "Противник промахнулся! Ваш ход!";
    }
    
    #endregion
    
    #region Обновление UI и статистики
    
    private void UpdateStats()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            PlayerStatsText.Text = $"🎯 Ваши выстрелы: {playerHits} попаданий, {playerMisses} промахов";
            OpponentStatsText.Text = $"💣 Выстрелы {_networkManager.OpponentName}: {opponentHits} попаданий, {opponentMisses} промахов";
        }
        else
            if (currentMode == GameMode.VsPlayer)
            {
                int ownHits = isPlayer2Turn ? computerHits : playerHits;
                int ownMisses = isPlayer2Turn ? computerMisses : playerMisses;
                int enemyHits = isPlayer2Turn ? playerHits : computerHits;
                int enemyMisses = isPlayer2Turn ? playerMisses : computerMisses;
                PlayerStatsText.Text = $"🎯 Ваши выстрелы: {ownHits} попаданий, {ownMisses} промахов";
                OpponentStatsText.Text = $"💣 Выстрелы противника: {enemyHits} попаданий, {enemyMisses} промахов";
            }
            else
            {
                PlayerStatsText.Text = $"🎯 Ваши выстрелы: {playerHits} попаданий, {playerMisses} промахов";
                OpponentStatsText.Text = $"💣 Выстрелы противника: {computerHits} попаданий, {computerMisses} промахов";
            }
    }

    private void UpdateStatusAndBoards()
    {
        if (!_isGameScreenVisible) return;
        if (_networkManager.NetworkMode != NetworkGameMode.InGame)
            if (currentMode == GameMode.VsPlayer)
                GameStatusText?.Text = isPlayer2Turn
                    ? "⚔️ ВАШ ХОД, ИГРОК 2! Атакуйте поле противника"
                    : "⚔️ ВАШ ХОД, ИГРОК 1! Атакуйте поле противника";
            else if (currentMode == GameMode.VsComputer)
                GameStatusText?.Text = playerTurn ? "⚔️ ВАШ ХОД! Атакуйте поле противника" : "💀 Ход противника...";
    
        string ownTitle = "🛡️ ВАШЕ ПОЛЕ";
        string enemyTitle = GetEnemyBoardTitle();
    
        OwnBoardTitle?.Text = ownTitle;
    
        EnemyBoardTitle?.Text = enemyTitle;
    
        UpdateBoards();
        UpdateStats();
    }
    
    private string GetEnemyBoardTitle()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            return $"🎯 ПОЛЕ {_networkManager.OpponentName.ToUpper()}";
        else if (currentMode == GameMode.VsPlayer)
            return isPlayer2Turn ? "🎯 ПОЛЕ ИГРОКА 1" : "🎯 ПОЛЕ ИГРОКА 2";
        else
            return "🎯 ПОЛЕ ПРОТИВНИКА";
    }

    private void UpdateBoard(Canvas canvas, GameBoard board, bool isEnemy)
    {
        if (canvas == null || board == null) return;
    
        canvas.Children.Clear();

        int cellSize = 40;
        int padding = 10;

        // Координаты
        for (int i = 0; i < board.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString()
            };
            letterText.Classes.Add("Coordinate");
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString()
            };
            numberText.Classes.Add("Coordinate");
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize / 2 - 7);
            canvas.Children.Add(numberText);
        }

        // Клетки
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
    
        // Принудительная отрисовка
        canvas.InvalidateVisual();
    }
    
    #endregion
    
    #region Создание игровых элементов

    private Control CreateGameCell(GameBoard board, int x, int y, int cellSize, bool isEnemy)
    {
        var border = new Border
        {
            Width = cellSize - 2,
            Height = cellSize - 2
        };
        border.Classes.Add("GameCell");

        var state = board.Grid[x, y];
    
        // Убедитесь, что для Sunk всегда используется класс "Sunk", даже если это поле противника
        if (state == CellState.Sunk)
            border.Classes.Add("Sunk");
        else if (isEnemy && _networkManager.NetworkMode == NetworkGameMode.InGame && state == CellState.Ship)
            border.Classes.Add("Empty");
        else
            border.Classes.Add(state switch
            {
                CellState.Empty => "Empty",
                CellState.Ship => isEnemy ? "Empty" : "Ship",
                CellState.Miss => "Miss",
                CellState.Hit => "Hit",
                CellState.Blocked => "Blocked",
                _ => "Empty"
            });

        var content = new Canvas { Width = cellSize - 2, Height = cellSize - 2 };

        if (board.Grid[x, y] == CellState.Ship && !isEnemy)
            DrawShipSegment(content, cellSize - 2);
        else if (board.Grid[x, y] == CellState.Miss)
            DrawMiss(content, cellSize - 2);
        else if (board.Grid[x, y] == CellState.Hit)
            DrawHit(content, cellSize - 2);
        else if (board.Grid[x, y] == CellState.Sunk)
            DrawSunk(content, cellSize - 2);
        else if (board.Grid[x, y] == CellState.Blocked)
            DrawBlocked(content, cellSize - 2);

        border.Child = content;

        if (isEnemy)
        {
            int cx = x, cy = y;
            bool canClick = false;
            
            if (_networkManager.NetworkMode == NetworkGameMode.InGame)
                canClick = playerTurn;
            else if (currentMode == GameMode.VsPlayer && _networkManager.NetworkMode == NetworkGameMode.None)
                canClick = playerTurn;
            else if (currentMode == GameMode.VsComputer)
                canClick = playerTurn;
            
            var cellState = board.Grid[cx, cy];
            bool cellAvailable = cellState == CellState.Empty || cellState == CellState.Ship;

            if (canClick && cellAvailable)
            {
                border.PointerPressed += async (s, e) => 
                {
                    if (_networkManager.NetworkMode == NetworkGameMode.InGame)
                        await OnNetworkGameCellClickAsync(cx, cy);
                    else
                        OnGameCellClick(cx, cy);
                };
                border.Cursor = new Cursor(StandardCursorType.Hand);
            
                border.PointerEntered += (s, e) =>
                {
                    if (cellState == CellState.Empty || cellState == CellState.Ship)
                        border.Opacity = 0.8;
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
    
    #endregion
}