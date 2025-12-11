using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using BattleShipGame.Models2;
using BattleShipGame.Networking;
using BattleShipGame.Logic;
using BattleShipGame.ViewModels;

namespace BattleShipGame.Views;

/// <summary>
/// Главное окно игры «Морской бой».
/// Поддерживает три режима:
/// • против компьютера (с тремя уровнями сложности),
/// • локальная игра на двоих,
/// • сетевая игра через собственный сервер.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Представление модели главного окна для привязки данных.
    /// </summary>
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;
    
    #region Поля и свойства

    /// <summary>Собственная игровая доска игрока.</summary>
    private GameBoard? _playerBoard;
    
    /// <summary>Доска компьютера (режим против ИИ).</summary>
    private GameBoard? _computerBoard;
    
    /// <summary>Доска соперника в сетевой игре.</summary>
    private GameBoard? _opponentBoard;

    /// <summary>Текущий режим игры (меню, против ПК, вдвоём, онлайн).</summary>
    private GameMode _currentMode = GameMode.Menu;

    /// <summary>Флаг, указывающий, чей сейчас ход в сетевой/локальной игре.</summary>
    private bool _playerTurn = true;
    
    /// <summary>Флаг хода второго игрока в локальном режиме «на двоих».</summary>
    private bool _isPlayer2Turn;

    /// <summary>Количество попаданий игрока.</summary>
    private int _playerHits;
    
    /// <summary>Количество промахов игрока.</summary>
    private int _playerMisses;
    
    /// <summary>Количество попаданий компьютера.</summary>
    private int _computerHits;
    
    /// <summary>Количество промахов компьютера.</summary>
    private int _computerMisses;
    
    /// <summary>Количество попаданий соперника (сетевая игра).</summary>
    private int _opponentHits;
    
    /// <summary>Количество промахов соперника (сетевая игра).</summary>
    private int _opponentMisses;

    // --------------------------------------------------------------------
    // Расстановка кораблей вручную
    // --------------------------------------------------------------------
    
    /// <summary>
    /// Список размеров кораблей, которые нужно разместить.
    /// Порядок: 4-палубный, два 3-палубных, три 2-палубных, четыре 1-палубных.
    /// </summary>
    private List<int> _shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };

    /// <summary>Индекс текущего размещаемого корабля в списке shipsToPlace.</summary>
    private int _currentShipIndex;
    
    /// <summary>Ориентация текущего корабля (true — горизонтально, false — вертикально).</summary>
    private bool _currentShipHorizontal = true;

    /// <summary>Доска, на которой сейчас происходит расстановка кораблей.</summary>
    private GameBoard? _placingBoard;
    
    /// <summary>
    /// Флаг, указывающий, какой игрок сейчас расставляет корабли в локальном режиме.
    /// true — расставляет первый игрок, false — второй.
    /// </summary>
    private bool _placingPlayer1Ships = true;
    
    // Боты
    /// <summary>Менеджер ботов для управления логикой ИИ противника.</summary>
    private readonly BotManager _botManager = new BotManager();
    
    /// <summary>Текущая сложность бота (легкая, средняя, сложная).</summary>
    private BotDifficulty _botDifficulty = BotDifficulty.Easy;
    
    // --------------------------------------------------------------------
    // Сетевые поля
    // --------------------------------------------------------------------
    
    /// <summary>Менеджер чата для сетевой игры.</summary>
    private ChatManager? _chatManager;
    
    /// <summary>Менеджер сетевой игры для обработки сетевых сообщений и состояния.</summary>
    private readonly NetworkGameManager _networkManager;
    
    /// <summary>Клиент для соединения с сетевым сервером.</summary>
    private readonly NetworkClient _networkClient = new NetworkClient();
    
    /// <summary>Флаг окончания игры.</summary>
    private bool _gameOver;
    
    // --------------------------------------------------------------------
    // UI-элементы игрового поля
    // --------------------------------------------------------------------
    
    /// <summary>Canvas для ручной расстановки кораблей.</summary>
    private Canvas? _placementCanvas;
    
    /// <summary>Левое поле — всегда своё (с видимыми кораблями).</summary>
    private Canvas? _ownCanvas;
    
    /// <summary>Правое поле — поле противника.</summary>
    private Canvas? _enemyCanvas;

    /// <summary>
    /// Флаг блокировки повторных атак при ожидании результата от сервера.
    /// Предотвращает множественные атаки во время обработки сетевого запроса.
    /// </summary>
    private bool _isProcessingNetworkAttack;
    
    /// <summary>Флаг обработки завершения игры.</summary>
    private bool _isProcessingGameOver;
    
    /// <summary>Дополнительный флаг обработки завершения игры для сетевого режима.</summary>
    private bool _isGameOverProcessing;
    
    /// <summary>Объект блокировки для синхронизации обработки завершения игры.</summary>
    private readonly object _gameOverLock = new object();
    
    #endregion


    #region Конструктор и инициализация

    /// <summary>
    /// Инициализирует главное окно игры.
    /// Задаёт заголовок, размеры, фон и запускает экран загрузки.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext == null)
        {
            DataContext = new MainWindowViewModel();
            Console.WriteLine($"[DEBUG] Created new DataContext");
        }
        else
        {
            Console.WriteLine($"[DEBUG] DataContext already exists: {DataContext.GetType().Name}");
        }
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.RequestGameReset += OnGameResetRequested;
        }
        InitializeEventHandlers();
        _networkManager = new NetworkGameManager(_networkClient);
        SubscribeToNetworkEvents();
        ShowLoadingScreen();
    }
    
    /// <summary>
    /// Инициализирует обработчики событий для UI-элементов.
    /// </summary>
    private void InitializeEventHandlers()
    {
        // Инициализация canvas ссылок
        _ownCanvas = OwnCanvas;
        _enemyCanvas = EnemyCanvas;
        _placementCanvas = PlacementCanvas;
        
        // Главное меню
        VsComputerButton.Click += (_, _) => ShowDifficultyWindow();
        VsPlayerButton.Click += (_, _) => StartGame(GameMode.VsPlayer);
        VsOnlineButton.Click += (_, _) => ShowNetworkConnectWindow();
        
        // Расстановка
        RotateShipButton.Click += (_, _) => RotateCurrentShip();
        RandomPlacementButton.Click += (_, _) => PlaceShipsRandomly();
        StartGameButton.Click += (_, _) => FinishPlacement();
        
        // Игровой экран
        NewGameButton.Click += (_, _) => OnNewGameClick();
        ToMenuButton.Click += (_, _) => OnToMenuClick();
    }
    
    #endregion
    
    #region Network Event Handlers
    
    /// <summary>
    /// Подписывается на события сетевого менеджера.
    /// Все обработчики выполняются в UI-потоке через Dispatcher.
    /// </summary>
    private void SubscribeToNetworkEvents()
    {
        _networkManager.StatusChanged += (status) => 
            Dispatcher.UIThread.Post(() => OnNetworkStatusChanged(status));
            
        _networkManager.PlayerTurnChanged += (isPlayerTurn) => 
            Dispatcher.UIThread.Post(() => OnPlayerTurnChanged(isPlayerTurn));
            
        _networkManager.GameStarted += (_, _) => 
            Dispatcher.UIThread.Post(OnNetworkGameStarted);
            
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
            Dispatcher.UIThread.Post(OnMatchFound);
            
        _networkManager.GameStartReceived += (playerTurn) => 
            Dispatcher.UIThread.Post(() => OnGameStartReceived(playerTurn));
            
        _networkManager.YourTurnReceived += () => 
            Dispatcher.UIThread.Post(OnYourTurn);
    
        _networkManager.YourTurnAgainReceived += () => 
            Dispatcher.UIThread.Post(OnYourTurnAgain);
    
        _networkManager.OpponentTurnReceived += () => 
            Dispatcher.UIThread.Post(OnOpponentTurn);
            
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
    
    /// <summary>
    /// Инициализирует игровые доски для сетевой игры.
    /// Получает доски из NetworkManager или создает новые при необходимости.
    /// </summary>
    private void InitializeNetworkGameBoards()
    {
        Console.WriteLine($"[DEBUG] Initializing network game boards...");
    
        // Получаем доски из NetworkManager
        _playerBoard = _networkManager.PlayerBoard;
        _opponentBoard = _networkManager.OpponentBoard;
    
        Console.WriteLine($"[DEBUG] playerBoard from manager: {_playerBoard != null}");
        Console.WriteLine($"[DEBUG] opponentBoard from manager: {_opponentBoard != null}");
    
        // Если доски все еще null, создаем новые
        _playerBoard ??= new GameBoard();
        _opponentBoard ??= new GameBoard();
        
        // Убедимся, что NetworkManager знает об этих досках
        _networkManager.PlayerBoard = _playerBoard;
        _networkManager.OpponentBoard = _opponentBoard;
    
        Console.WriteLine($"[DEBUG] Boards initialized successfully");
    }
    
    /// <summary>
    /// Обрабатывает нажатие кнопки "Новая игра".
    /// В сетевом режиме показывает диалог подтверждения.
    /// </summary>
    private void OnNewGameClick()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            ShowConfirmDialog(
                "Начать новую онлайн-игру?\nТекущая игра будет завершена.",
                () => {
                    _ = LeaveNetworkGameAsync();
                    ShowNetworkConnectWindow();
                }
            );
        else
            StartGame(_currentMode);
    }

    /// <summary>
    /// Обрабатывает нажатие кнопки "В главное меню".
    /// В сетевом режиме показывает диалог подтверждения.
    /// </summary>
    private void OnToMenuClick()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            ShowConfirmDialog(
                "Вернуться в главное меню?\nТекущая игра будет завершена.",
                () => {
                    _ = LeaveNetworkGameAsync();
                    ShowMainMenu();
                }
            );
        else
            ShowMainMenu();
    }
    
    /// <summary>
    /// Выполняет сброс игрового состояния.
    /// </summary>
    private void OnGameResetRequested()
    {
        ResetGameState();
    }
    
    /// <summary>
    /// Обрабатывает изменение сетевого статуса.
    /// </summary>
    /// <param name="status">Новый статус игры.</param>
    private void OnNetworkStatusChanged(string status)
    {
        ViewModel.GameStatus = status;
    }
    
    /// <summary>
    /// Обрабатывает изменение хода игрока в сетевой игре.
    /// </summary>
    /// <param name="isPlayerTurn">true если ход игрока, false если ход соперника.</param>
    private void OnPlayerTurnChanged(bool isPlayerTurn)
    {
        _playerTurn = isPlayerTurn;
        UpdateStatusAndBoards();
    }
    
    /// <summary>
    /// Обрабатывает начало сетевой игры.
    /// </summary>
    private void OnNetworkGameStarted()
    {
        StartNetworkGame();
    }
    
    /// <summary>
    /// Обрабатывает завершение сетевой игры.
    /// Защищено от повторной обработки с помощью флага _isProcessingGameOver.
    /// </summary>
    /// <param name="winnerName">Имя победителя.</param>
    /// <param name="iWon">true если текущий игрок победил.</param>
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
    
    /// <summary>
    /// Обрабатывает выход соперника из игры.
    /// </summary>
    /// <param name="message">Сообщение о выходе соперника.</param>
    private async void OnOpponentLeft(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => 
            ShowOpponentLeftDialog(message));
    }
    
    /// <summary>
    /// Обрабатывает отключение соперника.
    /// </summary>
    /// <param name="message">Сообщение об отключении.</param>
    private async void OnOpponentDisconnected(string message)
    {
        await Dispatcher.UIThread.InvokeAsync(() => 
            ShowOpponentDisconnectedDialog(message));
    }
    
    /// <summary>
    /// Обрабатывает потерю соединения с сервером.
    /// Сбрасывает состояние сетевой игры и возвращает в главное меню.
    /// </summary>
    /// <param name="message">Сообщение о потере соединения.</param>
    private void OnConnectionLost(string message)
    {
    
        // Сбрасываем состояние сетевой игры
        _ = LeaveNetworkGameAsync();
    
        Dispatcher.UIThread.Post(() => 
        {
            ViewModel.GameStatus = message;
            ShowMainMenu();
        });
    }
    
    /// <summary>
    /// Обрабатывает получение сообщения о успешном присоединении к игре.
    /// </summary>
    /// <param name="message">Сообщение о присоединении.</param>
    private void OnJoinedReceived(string message)
    {
        ViewModel.GameStatus = message;
    }
    
    /// <summary>
    /// Обрабатывает нахождение соперника для сетевой игры.
    /// </summary>
    private void OnMatchFound()
    {
        StartNetworkGame();
    }
    
    /// <summary>
    /// Обрабатывает получение сообщения о начале игры.
    /// </summary>
    /// <param name="isPlayerTurn">true если первый ход у текущего игрока.</param>
    private void OnGameStartReceived(bool isPlayerTurn)
    {
        _playerTurn = isPlayerTurn;
        ShowGameScreen();
    }
    
    /// <summary>
    /// Обрабатывает получение сообщения "Ваш ход".
    /// </summary>
    private void OnYourTurn()
    {
        _playerTurn = true;
        if (ViewModel.IsGameScreenVisible)
            UpdateStatusAndBoards();
    }

    /// <summary>
    /// Обрабатывает получение сообщения "Ваш ход снова" (после попадания).
    /// </summary>
    private void OnYourTurnAgain()
    {
        _playerTurn = true;
        if (ViewModel.IsGameScreenVisible)
            UpdateStatusAndBoards();
    }

    /// <summary>
    /// Обрабатывает получение сообщения "Ход соперника".
    /// </summary>
    private void OnOpponentTurn()
    {
        _playerTurn = false;
        if (ViewModel.IsGameScreenVisible)
            UpdateStatusAndBoards();
    }
    
    /// <summary>
    /// Обрабатывает получение результата атаки от сервера.
    /// </summary>
    /// <param name="x">X-координата атакованной клетки.</param>
    /// <param name="y">Y-координата атакованной клетки.</param>
    /// <param name="hit">true если было попадание.</param>
    /// <param name="sunk">true если корабль был потоплен.</param>
    /// <param name="gameOver">true если игра завершена.</param>
    /// <param name="isMyAttack">true если это атака текущего игрока.</param>
    /// <param name="data">Дополнительные данные атаки.</param>
    private void OnAttackResultReceived(int x, int y, bool hit, bool sunk, bool gameOver, bool isMyAttack, Dictionary<string, string> data)
    {
        HandleAttackResultMessage(x, y, hit, sunk, gameOver, isMyAttack, data);
    }
    
    #endregion
    
    #region Экран загрузки
    
    /// <summary>
    /// Показывает экран загрузки с анимацией прогресса.
    /// </summary>
    private async void ShowLoadingScreen()
    {
        ViewModel.ShowLoadingScreen();
        await ViewModel.SimulateLoadingAsync();
        ShowMainMenu();
    }
    
    #endregion
    
    #region Сетевое взаимодействие
    
    /// <summary>
    /// Подключается к сетевому серверу.
    /// </summary>
    /// <param name="hostname">Имя хоста сервера.</param>
    /// <param name="port">Порт сервера.</param>
    /// <param name="playerName">Имя игрока.</param>
    /// <returns>
    /// Кортеж (success, errorMessage):
    /// success - true если подключение успешно,
    /// errorMessage - сообщение об ошибке при неудаче.
    /// </returns>
    private async Task<(bool success, string errorMessage)> ConnectToServer(string hostname, int port, string playerName)
    {
        return await _networkManager.ConnectToServer(hostname, port, playerName);
    }
    
    /// <summary>
    /// Сбрасывает состояние расстановки кораблей к начальному.
    /// </summary>
    private void ResetPlacementState()
    {
        _shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
        _currentShipIndex = 0;
        _currentShipHorizontal = true;
        _placingPlayer1Ships = true;
    
        // Сброс состояния доски
        _playerBoard?.Clear();
        _computerBoard?.Clear();
        _opponentBoard?.Clear();
    }
    
    /// <summary>
    /// Начинает сетевую игру.
    /// Инициализирует состояние игры и переходит к расстановке кораблей.
    /// </summary>
    private void StartNetworkGame()
    {
        ResetPlacementState();
        _playerHits = 0;
        _playerMisses = 0;
        _opponentHits = 0;
        _opponentMisses = 0;
        _gameOver = false;
        _isProcessingNetworkAttack = false;
        _currentMode = GameMode.VsPlayer;
        
        InitializeNetworkGameBoards();
    
        _placingBoard = _playerBoard!; // Гарантированно не null после InitializeNetworkGameBoards
        _placingPlayer1Ships = true;
        _currentShipIndex = 0;
        _currentShipHorizontal = true;
        _playerTurn = false;
        _isPlayer2Turn = false;
    
        _chatManager = new ChatManager(_networkClient, _networkManager.PlayerName);
        Dispatcher.UIThread.Post(() => 
        {
            ShowShipPlacementScreen();
            ViewModel.PlacementStatus = $"Найден соперник: {_networkManager.OpponentName}! Начинаем расстановку...";
        });
    }
    
    /// <summary>
    /// Обрабатывает клик по ячейке в сетевой игре.
    /// Отправляет атаку на сервер если это ход игрока.
    /// </summary>
    /// <param name="x">X-координата ячейки.</param>
    /// <param name="y">Y-координата ячейки.</param>
    private async Task OnNetworkGameCellClickAsync(int x, int y)
    {
        Console.WriteLine($"[DEBUG] OnNetworkGameCellClickAsync: x={x}, y={y}, playerTurn={_playerTurn}");
    
        if (!_playerTurn || _isProcessingNetworkAttack)
        {
            Console.WriteLine($"[DEBUG] Attack rejected");
            return;
        }

        if (_opponentBoard == null)
        {
            Console.WriteLine($"[ERROR] opponentBoard is null");
            return;
        }
        
        var cellState = _opponentBoard.Grid[x, y];
        if (cellState != CellState.Empty && cellState != CellState.Ship)
        {
            Console.WriteLine($"[DEBUG] Cell already attacked");
            return;
        }

        _isProcessingNetworkAttack = true;
        await _networkManager.SendAttackAsync(x, y);
        _isProcessingNetworkAttack = false;
    }
    
    /// <summary>
    /// Покидает текущую сетевую игру.
    /// </summary>
    /// <param name="clearBoards">
    /// true - очищает игровые доски (при явном выходе),
    /// false - сохраняет доски (при завершении игры для показа результатов).
    /// </param>
    private async Task LeaveNetworkGameAsync(bool clearBoards = true)
    {
        Console.WriteLine($"[DEBUG] Leaving network game (clearBoards={clearBoards})...");
        
        _gameOver = true;
        
        await _networkManager.LeaveGameAsync();
        
        if (clearBoards)
        {
            _playerBoard = null;
            _opponentBoard = null;
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
    
    /// <summary>
    /// Показывает главное меню игры.
    /// Сбрасывает состояние игры и сетевые подключения.
    /// </summary>
    private void ShowMainMenu()
    {
        Console.WriteLine($"[DEBUG MainWindow] ShowMainMenu called");
        ViewModel.ShowMainMenuCommand.Execute(null);
        Console.WriteLine($"[DEBUG MainWindow] ShowMainMenu completed");
    }
    
    #endregion
    
    #region Окно выбора сложности

    /// <summary>
    /// Показывает окно выбора сложности для игры против компьютера.
    /// После выбора сложности начинает игру.
    /// </summary>
    private async void ShowDifficultyWindow()
    {
        var difficultyWindow = new DifficultyWindow();
        await difficultyWindow.ShowDialog(this);
    
        if (difficultyWindow.SelectedDifficulty.HasValue)
        {
            _botDifficulty = difficultyWindow.SelectedDifficulty.Value;
            _botManager.SetDifficulty(_botDifficulty);
            StartGame(GameMode.VsComputer);
        }
    }
    
    #endregion
    
    #region Сетевое подключение
    
    /// <summary>
    /// Показывает окно подключения к сетевой игре.
    /// После успешного подключения начинает поиск соперника.
    /// </summary>
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
                ViewModel.GameStatus = $"Подключение к серверу... Ищу соперника...";
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

    /// <summary>
    /// Начинает новую игру в указанном режиме.
    /// Инициализирует игровые доски и состояние игры.
    /// </summary>
    /// <param name="mode">Режим игры (против компьютера, локальный, сетевой).</param>
    private void StartGame(GameMode mode)
    {
        if (_networkManager.NetworkMode != NetworkGameMode.None) return;
        
        _currentMode = mode;
        _playerBoard = new GameBoard();
        _computerBoard = new GameBoard();
        _opponentBoard = null;
        _placingBoard = _playerBoard;
        _placingPlayer1Ships = true;
        _currentShipIndex = 0;
        _currentShipHorizontal = true;
        _playerTurn = true;
        _isPlayer2Turn = false;
        _playerHits = 0;
        _playerMisses = 0;
        _computerHits = 0;
        _computerMisses = 0;
        _opponentHits = 0;
        _opponentMisses = 0;
        _gameOver = false;
        if (mode == GameMode.VsComputer)
        {
            _botManager.SetDifficulty(_botDifficulty);
            _botManager.ResetAll();
        }
        ShowShipPlacementScreen();
    }
    
    /// <summary>
    /// Сбрасывает игровое состояние игры.
    /// </summary>
    private void ResetGameState()
    {
        _chatManager = null;
        _playerBoard = null;
        _computerBoard = null;
        _opponentBoard = null;
        _playerHits = 0;
        _playerMisses = 0;
        _computerHits = 0;
        _computerMisses = 0;
        _opponentHits = 0;
        _opponentMisses = 0;
        _gameOver = false;
        _shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
        _currentShipIndex = 0;
        _currentShipHorizontal = true;
        _placingPlayer1Ships = true;
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            if (_networkClient.IsConnected)
                _ = LeaveNetworkGameAsync();
        }
        else if (_networkClient.IsConnected)
            _networkClient.Disconnect();
    
        _currentMode = GameMode.Menu;
        ClearAllCanvases();
    }

    /// <summary>
    /// Очистка игровых канвасов.
    /// </summary>
    private void ClearAllCanvases()
    {
        try
        {
            Console.WriteLine($"[DEBUG] Clearing all canvases");
            Dispatcher.UIThread.Post(() =>
            {
                // Обновляем ссылки на canvas
                _ownCanvas = OwnCanvas;
                _enemyCanvas = EnemyCanvas;
                _placementCanvas = PlacementCanvas;
            
                int clearedCount = 0;
            
                if (_ownCanvas != null) 
                {
                    _ownCanvas.Children.Clear();
                    clearedCount++;
                    Console.WriteLine($"[DEBUG] OwnCanvas cleared");
                }
            
                if (_enemyCanvas != null) 
                {
                    _enemyCanvas.Children.Clear();
                    clearedCount++;
                    Console.WriteLine($"[DEBUG] EnemyCanvas cleared");
                }
            
                if (_placementCanvas != null) 
                {
                    _placementCanvas.Children.Clear();
                    clearedCount++;
                    Console.WriteLine($"[DEBUG] PlacementCanvas cleared");
                }
            
                Console.WriteLine($"[DEBUG] Cleared {clearedCount} canvases");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to clear canvases: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
        }
    }
    
    #endregion
    
    #region Расстановка кораблей
    
    /// <summary>
    /// Обновляет инструкции по расстановке кораблей в UI.
    /// </summary>
    private void UpdatePlacementInstructions()
    {
        ViewModel.PlacementInstruction = _currentShipIndex < _shipsToPlace.Count
            ? $"Размещаем корабль размером {_shipsToPlace[_currentShipIndex]} клеток\nПробел - повернуть, ЛКМ - разместить"
            : "Все корабли размещены!";
    }

    /// <summary>
    /// Отрисовывает canvas для расстановки кораблей.
    /// Отображает координатные оси и ячейки доски.
    /// </summary>
    private void RenderPlacementCanvas()
    {
        if (_placementCanvas == null) return;
        
        _placementCanvas.Children.Clear();

        int cellSize = 40;
        int padding = 10;

        // Координаты
        for (int i = 0; i < _placingBoard!.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString()
            };
            letterText.Classes.Add("Coordinate");
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize * 0.5 - 5);
            Canvas.SetTop(letterText, 0);
            _placementCanvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString()
            };
            numberText.Classes.Add("Coordinate");
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize * 0.5 - 7);
            _placementCanvas.Children.Add(numberText);
        }

        // Клетки
        for (int i = 0; i < _placingBoard.Size; i++)
        {
            for (int j = 0; j < _placingBoard.Size; j++)
            {
                var cell = CreatePlacementCell(i, j, cellSize);
                Canvas.SetLeft(cell, padding + i * cellSize);
                Canvas.SetTop(cell, padding + j * cellSize);
                _placementCanvas.Children.Add(cell);
            }
        }
    }
    
    /// <summary>
    /// Показывает экран расстановки кораблей.
    /// Инициализирует UI и подписывается на события клавиатуры.
    /// </summary>
    private void ShowShipPlacementScreen()
    {
        ViewModel.ShowPlacementScreen();
        
        string playerName = "Игрок";
        if (_currentMode == GameMode.VsPlayer && _networkManager.NetworkMode == NetworkGameMode.None)
            playerName = _placingPlayer1Ships ? "Игрок 1" : "Игрок 2";
        else if (_networkManager.NetworkMode == NetworkGameMode.InGame)
            playerName = "Вы";

        ViewModel.PlacementStatus = $"🚢 {playerName}: Расставьте корабли";
        UpdatePlacementInstructions();
        RenderPlacementCanvas();
        KeyDown += OnPlacementKeyDown;
    }

    /// <summary>
    /// Создает UI-элемент ячейки для расстановки кораблей.
    /// </summary>
    /// <param name="x">X-координата ячейки.</param>
    /// <param name="y">Y-координата ячейки.</param>
    /// <param name="cellSize">Размер ячейки в пикселях.</param>
    /// <returns>UI-элемент Border, представляющий ячейку.</returns>
    private Control CreatePlacementCell(int x, int y, int cellSize)
    {
        var border = new Border
        {
            Width = cellSize - 2,
            Height = cellSize - 2
        };
        border.Classes.Add("PlacementCell");

        if (_placingBoard!.Grid[x, y] == CellState.Ship)
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
        border.PointerPressed += (_, _) => OnPlacementCellClick(cx, cy);
        border.PointerEntered += (_, _) =>
        {
            if (_currentShipIndex < _shipsToPlace.Count)
            {
                HighlightShipPlacement(x, y, true);
            }
        };

        border.PointerExited += (_, _) =>
        {
            if (_currentShipIndex < _shipsToPlace.Count)
            {
                HighlightShipPlacement(x, y, false);
            }
        };

        return border;
    }

    /// <summary>
    /// Подсвечивает возможное размещение текущего корабля.
    /// </summary>
    /// <param name="x">X-координата начальной точки.</param>
    /// <param name="y">Y-координата начальной точки.</param>
    /// <param name="highlight">true - подсветить размещение, false - убрать подсветку.</param>
    private void HighlightShipPlacement(int x, int y, bool highlight)
    {
        if (_currentShipIndex >= _shipsToPlace.Count || _placingBoard == null) return;

        int shipSize = _shipsToPlace[_currentShipIndex];
        bool canPlace = _placingBoard.CanPlaceShip(x, y, shipSize, _currentShipHorizontal);

        for (int i = 0; i < shipSize; i++)
        {
            int px = _currentShipHorizontal ? x + i : x;
            int py = _currentShipHorizontal ? y : y + i;

            if (px >= 0 && px < _placingBoard.Size && py >= 0 && py < _placingBoard.Size)
            {
                var border = FindPlacementCellBorder(px, py);
                if (border != null && _placingBoard.Grid[px, py] != CellState.Ship)
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

    /// <summary>
    /// Находит Border ячейки по координатам.
    /// </summary>
    /// <param name="x">X-координата ячейки.</param>
    /// <param name="y">Y-координата ячейки.</param>
    /// <returns>Border ячейки или null если не найден.</returns>
    private Border? FindPlacementCellBorder(int x, int y)
    {
        if (_placementCanvas == null) return null;
        
        int cellSize = 40;
        int padding = 10;

        foreach (var child in _placementCanvas.Children)
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

    /// <summary>
    /// Обрабатывает клик по ячейке при расстановке кораблей.
    /// Пытается разместить текущий корабль в указанной позиции.
    /// </summary>
    /// <param name="x">X-координата ячейки.</param>
    /// <param name="y">Y-координата ячейки.</param>
    private void OnPlacementCellClick(int x, int y)
    {
        if (_currentShipIndex >= _shipsToPlace.Count || _placingBoard == null) return;

        int shipSize = _shipsToPlace[_currentShipIndex];
        var ship = new Ship(shipSize, _currentShipHorizontal);

        if (_placingBoard.PlaceShip(ship, x, y))
        {
            _currentShipIndex++;
            RenderPlacementCanvas();
            UpdatePlacementInstructions();

            if (_currentShipIndex >= _shipsToPlace.Count)
            {
                ViewModel.PlacementStatus = "✅ Все корабли размещены! Нажмите 'Начать игру'";
                StartGameButton.IsEnabled = true;
            }
        }
    }

    /// <summary>
    /// Размещает корабли случайным образом на текущей доске.
    /// </summary>
    private void PlaceShipsRandomly()
    {
        if (_placingBoard == null) return;
        
        _placingBoard.Clear();
        _placingBoard.PlaceShipsRandomly();
        _currentShipIndex = _shipsToPlace.Count;
        RenderPlacementCanvas();
        UpdatePlacementInstructions();
        ViewModel.PlacementStatus = "✅ Все корабли размещены! Нажмите 'Начать игру'";
        EnableStartButton();
    }
    
    /// <summary>
    /// Активирует кнопку начала игры в UI.
    /// </summary>
    private void EnableStartButton()
    {
        ViewModel.IsStartGameButtonEnabled = true;
    }

    /// <summary>
    /// Обрабатывает нажатие клавиш при расстановке кораблей.
    /// Пробел - повернуть корабль.
    /// </summary>
    private void OnPlacementKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) RotateCurrentShip();
    }

    /// <summary>
    /// Поворачивает текущий размещаемый корабль.
    /// Меняет ориентацию с горизонтальной на вертикальную и наоборот.
    /// </summary>
    private void RotateCurrentShip()
    {
        _currentShipHorizontal = !_currentShipHorizontal;
    }

    /// <summary>
    /// Завершает расстановку кораблей и начинает игру.
    /// В сетевом режиме отправляет расстановку на сервер.
    /// </summary>
    private async void FinishPlacement()
    {
        KeyDown -= OnPlacementKeyDown;

        if (_currentMode == GameMode.VsPlayer && _networkManager.NetworkMode == NetworkGameMode.None && _placingPlayer1Ships)
        {
            _placingPlayer1Ships = false;
            _placingBoard = _computerBoard; // Может быть null, но это обрабатывается в ShowShipPlacementScreen
            _currentShipIndex = 0;
            _currentShipHorizontal = true;
            ShowShipPlacementScreen();
        }
        else if (_currentMode == GameMode.VsComputer)
        {
            if (_computerBoard != null)
                _computerBoard.PlaceShipsRandomly();
            ShowGameScreen();
        }
        else if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            await _networkManager.SendShipPlacementAsync(_placingBoard!);
        
            ViewModel.GameStatus = "Корабли расставлены! Ждем соперника...";
        }
        else
            ShowGameScreen();
    }
    
    #endregion
    
    #region Игровой процесс - основной экран
    
    /// <summary>
    /// Показывает основной игровой экран с двумя полями.
    /// Инициализирует доски и чат для сетевой игры.
    /// </summary>
    private void ShowGameScreen()
    {
        ViewModel.ShowGameScreen();
        _isPlayer2Turn = false;
    
        // Инициализируем доски если они null
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            if (_playerBoard == null)
                _playerBoard = _networkManager.PlayerBoard;
            if (_opponentBoard == null)
                _opponentBoard = _networkManager.OpponentBoard;
        }
    
        if (_networkManager.NetworkMode == NetworkGameMode.InGame && _chatManager != null)
        {
            _chatManager = new ChatManager(_networkClient, _networkManager.PlayerName);
            _networkManager.SetChatManager(_chatManager);
            var chatControl = _chatManager.CreateChatControl();
            ChatContainer.Content = chatControl;
        }
        else
            ChatContainer.Content = null;
    
        UpdateStatusAndBoards();
    }
    
    #endregion
    
    #region Обработка кликов по ячейкам
    
    /// <summary>
    /// Обрабатывает клик по ячейке игрового поля.
    /// Выполняет атаку в зависимости от режима игры.
    /// </summary>
    /// <param name="x">X-координата ячейки.</param>
    /// <param name="y">Y-координата ячейки.</param>
    private async void OnGameCellClick(int x, int y)
    {
        if (_networkManager.NetworkMode != NetworkGameMode.None) return;
        
        if (_currentMode == GameMode.VsPlayer)
        {
            if (!_playerTurn) return;
            
            if (_playerBoard == null || _computerBoard == null) return;
            
            GameBoard targetBoard = (_currentMode == GameMode.VsPlayer && _isPlayer2Turn) ? _playerBoard : _computerBoard;
            var (hit, sunk, gameOver) = targetBoard.Attack(x, y);

            if (targetBoard.Grid[x, y] == CellState.Miss ||
                targetBoard.Grid[x, y] == CellState.Hit ||
                targetBoard.Grid[x, y] == CellState.Sunk)
            {
                if (hit)
                {
                    (_isPlayer2Turn ? ref _computerHits : ref _playerHits)++;

                    SoundManager.PlayHit();

                    if (sunk)
                    {
                        SoundManager.PlaySunk();
                        
                        ViewModel.GameStatus = gameOver
                            ? $"🎉🏆️ ПОБЕДА! {(_isPlayer2Turn ? "Игрок 2" : "Игрок 1")} потопил весь флот!"
                            : $"💥 {(_isPlayer2Turn ? "Игрок 2" : "Игрок 1")} потопил корабль!";

                        if (gameOver)
                        {
                            if (_isPlayer2Turn)
                                SoundManager.PlayLose();
                            else
                                SoundManager.PlayWin();
                            _playerTurn = false;
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
                        ViewModel.GameStatus = $"🔥 {(_isPlayer2Turn ? "Игрок 2" : "Игрок 1")} попал! Стреляет снова!";
                    
                    UpdateStats();
                    UpdateBoards();
                    await Task.Delay(500);
                    return;
                }
                else if (targetBoard.Grid[x, y] == CellState.Miss)
                {
                    (_isPlayer2Turn ? ref _computerMisses : ref _playerMisses)++;
                    SoundManager.PlayMiss();
                    ViewModel.GameStatus = $"💧 {(_isPlayer2Turn ? "Игрок 2" : "Игрок 1")} промахнулся! Ход переходит к {(_isPlayer2Turn ? "Игроку 1" : "Игроку 2")}";
                    UpdateStats();
                    UpdateBoards();
                    await Task.Delay(1200);
                    _isPlayer2Turn = !_isPlayer2Turn;
                    UpdateStatusAndBoards();
                    return;
                }
                
                UpdateBoards();
            }
        }
        else
        {
            // Режим против компьютера
            if (!_playerTurn || _computerBoard == null) return;
            var (hit, sunk, gameOver) = _computerBoard.Attack(x, y);
            if (hit)
            {
                _playerHits++;
                SoundManager.PlayHit();
                if (sunk)
                {
                    SoundManager.PlaySunk();
                    
                    ViewModel.GameStatus = gameOver
                        ? "🎉 ПОБЕДА! Вы потопили весь флот противника!"
                        : "💥 Корабль потоплен! Продолжайте атаку!";
                    if (gameOver)
                    {
                        SoundManager.PlayWin();
                        _playerTurn = false;
                        ShowGameOverDialog(true, "Вы");
                    }
                }
                else
                    ViewModel.GameStatus = "🔥 ПОПАДАНИЕ! Атакуйте снова!";
                UpdateStats();
                UpdateBoards();
            }
            else if (_computerBoard.Grid[x, y] == CellState.Miss)
            {
                _playerMisses++;
                SoundManager.PlayMiss();
                ViewModel.GameStatus = "💧 Промах! Ход переходит к противнику...";
                UpdateStats();
                UpdateBoards();
                _playerTurn = false;
                await Task.Delay(800);
                if (_botDifficulty == BotDifficulty.Easy)
                    await ComputerTurn();
                else
                    await ComputerTurnSmart();
            }
        }
    }
    
   /// <summary>
   /// Обновляет отображение игровых досок.
   /// Определяет какие доски отображать в зависимости от режима игры.
   /// </summary>
   private void UpdateBoards()
   {
       if (!ViewModel.IsGameScreenVisible) 
       {
           Console.WriteLine("[DEBUG] Game screen not visible, skipping UpdateBoards");
           return;
       }
       
       // Обновляем ссылки на canvas
       _ownCanvas = OwnCanvas;
       _enemyCanvas = EnemyCanvas;
       
       if (_ownCanvas == null || _enemyCanvas == null)
       {
           Console.WriteLine("[WARNING] Canvas not found in UpdateBoards");
           return;
       }
       
       GameBoard? ownBoard;
       GameBoard? enemyBoard;
       
       try
       {
           if (_networkManager.NetworkMode == NetworkGameMode.InGame)
           {
               ownBoard = _playerBoard;
               enemyBoard = _opponentBoard;
               
               Console.WriteLine($"[DEBUG] UpdateBoards - Network game mode detected");
               Console.WriteLine($"[DEBUG] playerBoard: {_playerBoard != null}, opponentBoard: {_opponentBoard != null}");
               Console.WriteLine($"[DEBUG] Game over flag: {_gameOver}, isGameOverProcessing: {_isGameOverProcessing}");
               
               // При завершении игры показываем все клетки
               if (_gameOver && !_isGameOverProcessing)
               {
                   Console.WriteLine($"[DEBUG] Final board state - showing all cells");
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
           else if (_currentMode == GameMode.VsPlayer)
           {
               ownBoard = _isPlayer2Turn ? _computerBoard : _playerBoard;
               enemyBoard = _isPlayer2Turn ? _playerBoard : _computerBoard;
           }
           else // GameMode.VsComputer
           {
               ownBoard = _playerBoard;
               enemyBoard = _computerBoard;
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
           
           if (_gameOver && !_isProcessingGameOver)
           {
               Console.WriteLine($"[DEBUG] Final board state before drawing:");
               Console.WriteLine($"[DEBUG] Own board size: {ownBoard.Size}, Enemy board size: {enemyBoard.Size}");
           }
           
           UpdateBoard(_ownCanvas, ownBoard, false);
           UpdateBoard(_enemyCanvas, enemyBoard, true);
           
           Console.WriteLine($"[DEBUG] UpdateBoards completed successfully");
       }
       catch (Exception ex)
       {
           Console.WriteLine($"[ERROR] Exception in UpdateBoards: {ex.Message}");
           Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
       }
   }
   
   /// <summary>
   /// Принудительно перерисовывает доски после завершения игры.
   /// Обеспечивает корректное отображение финального состояния.
   /// </summary>
   /// <param name="isMyAttack">true если завершение произошло после атаки игрока.</param>
   private async Task ForceRedrawAfterGameOver(bool isMyAttack)
   {
       Console.WriteLine($"[DEBUG] ForceRedrawAfterGameOver called, isMyAttack={isMyAttack}");
       
       for (int i = 0; i < 5; i++)
       {
           if (ViewModel.IsGameScreenVisible)
           {
               await Dispatcher.UIThread.InvokeAsync(() => 
               {
                   UpdateBoards();
                   UpdateStats();
                   
                   _ownCanvas = OwnCanvas;
                   _enemyCanvas = EnemyCanvas;
                
                   if (_ownCanvas != null)
                   {
                       _ownCanvas.InvalidateVisual();
                       _ownCanvas.InvalidateMeasure();
                       _ownCanvas.InvalidateArrange();
                   }
                
                   if (_enemyCanvas != null)
                   {
                       _enemyCanvas.InvalidateVisual();
                       _enemyCanvas.InvalidateMeasure();
                       _enemyCanvas.InvalidateArrange();
                   }
               }, DispatcherPriority.Render);
            
               await Task.Delay(50);
           }
       }
    
       Console.WriteLine($"[DEBUG] ForceRedrawAfterGameOver completed");
   }
    
    #endregion
    
    #region Логика ботов
    
    /// <summary>
    /// Выполняет ход компьютера с простой логикой (случайные атаки).
    /// </summary>
    private async Task ComputerTurn()
    {
        if (_playerBoard == null) return;
        
        bool continueTurn = true;

        while (continueTurn && !_playerTurn && !_gameOver)
        {
            var result = await _botManager.MakeSimpleTurn(
                _playerBoard,
                HandleBotAttackResult
            );
            
            continueTurn = result is { ContinueTurn: true, GameOver: false };
            _gameOver = result.GameOver;
            
            if (continueTurn && !_gameOver)
            {
                await Task.Delay(500);
            }
            
            if (!continueTurn && !_gameOver)
            {
                _playerTurn = true;
                ViewModel.GameStatus = "⚔️ ВАШ ХОД! Атакуйте поле противника!";
                UpdateStatusAndBoards();
            }
            if (_gameOver)
            {
                _playerTurn = false;
                continueTurn = false;
            }
        }
    }

    /// <summary>
    /// Выполняет ход компьютера с продвинутой логикой.
    /// Использует алгоритмы поиска кораблей после попадания.
    /// </summary>
    private async Task ComputerTurnSmart()
    {
        if (_playerBoard == null) return;
        
        bool continueTurn = true;

        while (continueTurn && !_playerTurn && !_gameOver)
        {
            var result = await _botManager.MakeSmartTurn(
                _playerBoard,
                HandleBotAttackResult
            );
            
            continueTurn = result is { ContinueTurn: true, GameOver: false };
            _gameOver = result.GameOver;
            
            if (continueTurn && !_gameOver)
                await Task.Delay(500);
            
            if (!continueTurn && !_gameOver)
            {
                _playerTurn = true;
                ViewModel.GameStatus = "⚔️ ВАШ ХОД! Атакуйте поле противника!";
                UpdateStatusAndBoards();
            }
            if (_gameOver)
            {
                _playerTurn = false;
                continueTurn = false;
            }
        }
    }

    /// <summary>
    /// Обрабатывает результат атаки бота.
    /// Обновляет статистику, состояние игры и UI.
    /// </summary>
    /// <param name="x">X-координата атаки.</param>
    /// <param name="y">Y-координата атаки.</param>
    /// <param name="hit">true если попадание.</param>
    /// <param name="sunk">true если корабль потоплен.</param>
    /// <param name="gameOver">true если игра завершена.</param>
    private void HandleBotAttackResult(int x, int y, bool hit, bool sunk, bool gameOver)
    {
        _gameOver = gameOver;
    
        if (hit)
        {
            _computerHits++;
            SoundManager.PlayHit();

            if (sunk)
            {
                SoundManager.PlaySunk();
            
                ViewModel.GameStatus = gameOver
                    ? "💀 ПОРАЖЕНИЕ! Противник уничтожил ваш флот!"
                    : "⚠️ Противник потопил ваш корабль!";

                if (gameOver)
                {
                    SoundManager.PlayLose();
                    _playerTurn = false;
                    _gameOver = true;
                    Dispatcher.UIThread.Post(() => 
                    {
                        ShowGameOverDialog(false, "Противник");
                    }, DispatcherPriority.Background);
                }
            }
            else
                ViewModel.GameStatus = "💥 Противник попал в ваш корабль!";
        }
        else
        {
            _computerMisses++;
            SoundManager.PlayMiss();
            ViewModel.GameStatus = "⚔️ Противник промахнулся! ВАШ ХОД!";
        }
        UpdateStats();
        UpdateBoards();
    }
    
    #endregion
    
    #region Диалоговые окна
    
    /// <summary>
    /// Показывает диалог подтверждения действия.
    /// </summary>
    /// <param name="message">Сообщение диалога.</param>
    /// <param name="onConfirm">Действие при подтверждении.</param>
    private async void ShowConfirmDialog(string message, Action onConfirm)
    {
        var confirmWindow = new ConfirmDialogWindow
        {
            Message = message
        };
        confirmWindow.Message = message;
    
        var result = await confirmWindow.ShowDialog<bool?>(this);
    
        if (result.HasValue && result.Value)
            onConfirm.Invoke();
    }
    
    /// <summary>
    /// Показывает диалог завершения локальной игры.
    /// </summary>
    /// <param name="isWin">true если победа игрока.</param>
    /// <param name="winnerName">Имя победителя.</param>
    private async void ShowGameOverDialog(bool isWin, string winnerName)
    {
        var gameOverWindow = new GameOverWindow();
        gameOverWindow.IsWin = isWin;
        gameOverWindow.WinnerName = winnerName;
    
        await gameOverWindow.ShowDialog(this);
    
        if (gameOverWindow.Result.HasValue)
            if (gameOverWindow.Result.Value == GameOverResult.NewGame)
                StartGame(_currentMode);
            else if (gameOverWindow.Result.Value == GameOverResult.MainMenu)
                ShowMainMenu();
    }
    
    /// <summary>
    /// Показывает диалог завершения сетевой игры.
    /// </summary>
    /// <param name="winnerName">Имя победителя.</param>
    /// <param name="iWon">true если победа текущего игрока.</param>
    private async Task ShowNetworkGameOverDialog(string winnerName, bool iWon)
   {
       Console.WriteLine($"[DEBUG] ShowNetworkGameOverDialog: winner={winnerName}, iWon={iWon}");
       
       if (_isGameOverProcessing && _gameOver)
       {
           Console.WriteLine($"[DEBUG] Dialog already showing or game over processed, skipping");
           return;
       }

       if (ViewModel.GameStatus != null) 
       {
           ViewModel.GameStatus = iWon 
               ? "🎉 ПОЗДРАВЛЯЕМ! Вы победили!" 
               : $"💀 ПОРАЖЕНИЕ! Победил {winnerName}";
       }
       
       _isGameOverProcessing = true;
       _gameOver = true;
       _playerTurn = false;
       
       UpdateBoards();
       UpdateStats();
       
       _ownCanvas = OwnCanvas;
       _enemyCanvas = EnemyCanvas;
       
       _ownCanvas?.InvalidateVisual();
       _enemyCanvas?.InvalidateVisual();
       
       await Task.Delay(100);
       
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
                   await LeaveNetworkGameAsync();
                   ShowNetworkConnectWindow();
               }
               else if (result.Value == NetworkGameOverResult.MainMenu)
               {
                   await LeaveNetworkGameAsync();
                   ShowMainMenu();
               }
           }
           else
           {
               await LeaveNetworkGameAsync();
               ShowMainMenu();
           }
       }
       finally
       {
           this.IsEnabled = true;
           _isGameOverProcessing = false;
       }
   }

    
    /// <summary>
    /// Показывает диалог о выходе соперника из игры.
    /// </summary>
    /// <param name="message">Сообщение о выходе.</param>
    private async void ShowOpponentLeftDialog(string message)
    {
        var opponentWindow = new OpponentDisconnectWindow
        {
            Message = message
        };
        opponentWindow.Message = message;
        opponentWindow.Title = "Соперник покинул игру";
    
        var result = await opponentWindow.ShowDialog<bool?>(this);
    
        if (result.HasValue && result.Value)
        {
            await LeaveNetworkGameAsync();
            ShowMainMenu();
        }
    }
    
    /// <summary>
    /// Показывает диалог об отключении соперника.
    /// </summary>
    /// <param name="message">Сообщение об отключении.</param>
    private async void ShowOpponentDisconnectedDialog(string message)
    {
        var opponentWindow = new OpponentDisconnectWindow
        {
            Message = message
        };
        opponentWindow.Message = message;
        opponentWindow.Title = "Соединение потеряно";
    
        var result = await opponentWindow.ShowDialog<bool?>(this);
    
        if (result.HasValue && result.Value)
        {
            await LeaveNetworkGameAsync();
            ShowMainMenu();
        }
    }
    
    #endregion
    
    #region Обработка сетевых сообщений

    /// <summary>
    /// Обрабатывает сообщение с результатом атаки от сервера.
    /// Обновляет состояние доски, статистику и UI.
    /// При завершении игры показывает диалог.
    /// </summary>
    /// <param name="x">X-координата атаки.</param>
    /// <param name="y">Y-координата атаки.</param>
    /// <param name="hit">true если попадание.</param>
    /// <param name="sunk">true если корабль потоплен.</param>
    /// <param name="gameOver">true если игра завершена.</param>
    /// <param name="isMyAttack">true если атака текущего игрока.</param>
    /// <param name="data">Дополнительные данные атаки.</param>
    private async void HandleAttackResultMessage(int x, int y, bool hit, bool sunk, bool gameOver, bool isMyAttack, Dictionary<string, string> data)
   {
       Console.WriteLine($"[DEBUG] ATTACK_RESULT: ({x},{y}), hit={hit}, sunk={sunk}, gameOver={gameOver}, isMyAttack={isMyAttack}");

       // Защита от повторной обработки при завершении игры
       if (_gameOver && _isGameOverProcessing)
       {
           Console.WriteLine($"[DEBUG] Game already over or processing, ignoring attack result");
           return;
       }
       
       // Гарантируем, что доски инициализированы
       if (_networkManager.NetworkMode == NetworkGameMode.InGame)
       {
           if (_playerBoard == null || _opponentBoard == null)
           {
               Console.WriteLine($"[WARNING] Boards are null, initializing...");
               InitializeNetworkGameBoards();
           }
       }

       if (!ViewModel.IsGameScreenVisible)
       {
           Console.WriteLine($"[DEBUG] Game screen not visible, ignoring attack result");
           return;
       }
       
       GameBoard? targetBoard = isMyAttack ? _opponentBoard : _playerBoard;

       if (targetBoard == null)
       {
           Console.WriteLine($"[ERROR] Target board is null in HandleAttackResultMessage");
           return;
       }
       
       if (hit)
       {
           targetBoard.Grid[x, y] = sunk ? CellState.Sunk : CellState.Hit;
           
           if (isMyAttack) _playerHits++;
           else _opponentHits++;
           
           SoundManager.PlayHit();
           
           if (sunk)
           {
               SoundManager.PlaySunk();
               
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
                               // Помечаем ВСЕ клетки корабля как Sunk
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
           if (isMyAttack) _playerMisses++;
           else _opponentMisses++;
           SoundManager.PlayMiss();
       }
       
       // Обновление статуса
       UpdateGameStatus(isMyAttack, hit, sunk, gameOver);
       
       // Обновление UI
       if (ViewModel.IsGameScreenVisible)
       {
           UpdateStats();
           UpdateBoards();
       }
       
       if (gameOver)
       {
           _playerTurn = false;
           _gameOver = true;
           
           if (isMyAttack)
               SoundManager.PlayWin();
           else
               SoundManager.PlayLose();
           
           Console.WriteLine($"[DEBUG] Game over! Winner: {(isMyAttack ? "You" : _networkManager.OpponentName)}");
           
           if (ViewModel.IsGameScreenVisible)
           {
               await Task.Delay(100);
               UpdateBoards();
               await Task.Delay(100);
               UpdateBoards();
           }
           await ForceRedrawAfterGameOver(isMyAttack);
           await Task.Delay(800);
           
           await Dispatcher.UIThread.InvokeAsync(async () => 
           {
               if (ViewModel.IsGameScreenVisible)
               {
                   await ShowNetworkGameOverDialog(
                       isMyAttack ? _networkManager.PlayerName : _networkManager.OpponentName, 
                       isMyAttack
                   );
               }
           });
       }
   }


    /// <summary>
    /// Обновляет статус игры в зависимости от результата атаки.
    /// </summary>
    /// <param name="isMyAttack">true если атака текущего игрока.</param>
    /// <param name="hit">true если попадание.</param>
    /// <param name="sunk">true если корабль потоплен.</param>
    /// <param name="gameOver">true если игра завершена.</param>
    private void UpdateGameStatus(bool isMyAttack, bool hit, bool sunk, bool gameOver)
    {
        if (ViewModel.GameStatus == null) return;
        
        if (gameOver)
            ViewModel.GameStatus = isMyAttack ? "🎉 ПОБЕДА!" : "💀 ПОРАЖЕНИЕ!";
        else if (sunk)
            ViewModel.GameStatus = isMyAttack 
                ? "💥 Корабль потоплен! Стреляйте снова!" 
                : "⚠️ Противник потопил ваш корабль!";
        else if (hit)
            ViewModel.GameStatus = isMyAttack 
                ? "🔥 ПОПАДАНИЕ! Стреляйте снова!" 
                : "💥 Противник попал в ваш корабль!";
        else
            ViewModel.GameStatus = isMyAttack 
                ? "💧 Промах! Ход переходит к сопернику..." 
                : "Противник промахнулся! Ваш ход!";
    }
    
    #endregion
    
    #region Обновление UI и статистики
    
    /// <summary>
    /// Обновляет отображение статистики выстрелов в UI.
    /// </summary>
    private void UpdateStats()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            ViewModel.PlayerStats = $"🎯 Ваши выстрелы: {_playerHits} попаданий, {_playerMisses} промахов";
            ViewModel.OpponentStats = $"💣 Выстрелы {_networkManager.OpponentName}: {_opponentHits} попаданий, {_opponentMisses} промахов";
        }
        else
            if (_currentMode == GameMode.VsPlayer)
            {
                int ownHits = _isPlayer2Turn ? _computerHits : _playerHits;
                int ownMisses = _isPlayer2Turn ? _computerMisses : _playerMisses;
                int enemyHits = _isPlayer2Turn ? _playerHits : _computerHits;
                int enemyMisses = _isPlayer2Turn ? _playerMisses : _computerMisses;
                ViewModel.PlayerStats = $"🎯 Ваши выстрелы: {ownHits} попаданий, {ownMisses} промахов";
                ViewModel.OpponentStats = $"💣 Выстрелы противника: {enemyHits} попаданий, {enemyMisses} промахов";
            }
            else
            {
                ViewModel.PlayerStats = $"🎯 Ваши выстрелы: {_playerHits} попаданий, {_playerMisses} промахов";
                ViewModel.OpponentStats = $"💣 Выстрелы противника: {_computerHits} попаданий, {_computerMisses} промахов";
            }
    }

    /// <summary>
    /// Обновляет статус игры и игровые доски.
    /// Вызывается при изменении хода или состояния игры.
    /// </summary>
    private void UpdateStatusAndBoards()
    {
        if (!ViewModel.IsGameScreenVisible) return;
        if (_networkManager.NetworkMode != NetworkGameMode.None)
            if (_currentMode == GameMode.VsPlayer)
                ViewModel.GameStatus = _isPlayer2Turn
                    ? "⚔️ ВАШ ХОД, ИГРОК 2! Атакуйте поле противника"
                    : "⚔️ ВАШ ХОД, ИГРОК 1! Атакуйте поле противника";
            else if (_currentMode == GameMode.VsComputer)
                ViewModel.GameStatus = _playerTurn ? "⚔️ ВАШ ХОД! Атакуйте поле противника" : "💀 Ход противника...";
    
        ViewModel.OwnBoardTitle = "🛡️ ВАШЕ ПОЛЕ";
        ViewModel.EnemyBoardTitle = GetEnemyBoardTitle();
    
        UpdateBoards();
        UpdateStats();
    }
    
    /// <summary>
    /// Возвращает заголовок для поля противника в зависимости от режима игры.
    /// </summary>
    /// <returns>Заголовок поля противника.</returns>
    private string GetEnemyBoardTitle()
    {
        if (_networkManager.NetworkMode == NetworkGameMode.InGame)
        {
            var opponentName = _networkManager.OpponentName;
            if (string.IsNullOrEmpty(opponentName))
                return "🎯 ПОЛЕ ПРОТИВНИКА";
            else
                return $"🎯 ПОЛЕ {opponentName.ToUpper()}";
        }
        else if (_currentMode == GameMode.VsPlayer)
            return _isPlayer2Turn ? "🎯 ПОЛЕ ИГРОКА 1" : "🎯 ПОЛЕ ИГРОКА 2";
        else
            return "🎯 ПОЛЕ ПРОТИВНИКА";
    }

    /// <summary>
    /// Обновляет отображение игровой доски на указанном Canvas.
    /// </summary>
    /// <param name="canvas">Canvas для отрисовки доски.</param>
    /// <param name="board">Игровая доска.</param>
    /// <param name="isEnemy">true если это поле противника.</param>
    private void UpdateBoard(Canvas canvas, GameBoard board, bool isEnemy)
    {
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
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize * 0.5 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString()
            };
            numberText.Classes.Add("Coordinate");
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize * 0.5 - 7);
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

    /// <summary>
    /// Создает UI-элемент игровой ячейки.
    /// </summary>
    /// <param name="board">Игровая доска.</param>
    /// <param name="x">X-координата ячейки.</param>
    /// <param name="y">Y-координата ячейки.</param>
    /// <param name="cellSize">Размер ячейки в пикселях.</param>
    /// <param name="isEnemy">true если это ячейка поля противника.</param>
    /// <returns>UI-элемент Border, представляющий игровую ячейку.</returns>
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
                canClick = _playerTurn;
            else if (_currentMode == GameMode.VsPlayer && _networkManager.NetworkMode == NetworkGameMode.None)
                canClick = _playerTurn;
            else if (_currentMode == GameMode.VsComputer)
                canClick = _playerTurn;
            
            var cellState = board.Grid[cx, cy];
            bool cellAvailable = cellState == CellState.Empty || cellState == CellState.Ship;

            if (canClick && cellAvailable)
            {
                border.PointerPressed += async (_, _) => 
                {
                    if (_networkManager.NetworkMode == NetworkGameMode.InGame)
                        await OnNetworkGameCellClickAsync(cx, cy);
                    else
                        OnGameCellClick(cx, cy);
                };
                border.Cursor = new Cursor(StandardCursorType.Hand);
            
                border.PointerEntered += (_, _) =>
                {
                    if (cellState == CellState.Empty || cellState == CellState.Ship)
                        border.Opacity = 0.8;
                };
                border.PointerExited += (_, _) =>
                {
                    border.Opacity = 1.0;
                };
            }
        }

        return border;
    }

    /// <summary>
    /// Рисует сегмент корабля на Canvas.
    /// </summary>
    /// <param name="canvas">Canvas для рисования.</param>
    /// <param name="size">Размер области рисования.</param>
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

    /// <summary>
    /// Рисует отметку промаха на Canvas.
    /// </summary>
    /// <param name="canvas">Canvas для рисования.</param>
    /// <param name="size">Размер области рисования.</param>
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

    /// <summary>
    /// Рисует отметку попадания на Canvas.
    /// </summary>
    /// <param name="canvas">Canvas для рисования.</param>
    /// <param name="size">Размер области рисования.</param>
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

    /// <summary>
    /// Рисует отметку потопленного корабля на Canvas.
    /// </summary>
    /// <param name="canvas">Canvas для рисования.</param>
    /// <param name="size">Размер области рисования.</param>
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

    /// <summary>
    /// Рисует отметку заблокированной клетки на Canvas.
    /// </summary>
    /// <param name="canvas">Canvas для рисования.</param>
    /// <param name="size">Размер области рисования.</param>
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