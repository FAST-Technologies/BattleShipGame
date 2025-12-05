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
using TestAvalonia.Models;

namespace TestAvalonia.Views;

public partial class MainWindow : Window
{
    private GameBoard playerBoard;
    private GameBoard computerBoard;
    private TextBlock statusText;
    private TextBlock playerStatsText;
    private TextBlock computerStatsText;
    private Panel mainContent;
    private GameMode currentMode = GameMode.Menu;
    private bool playerTurn = true;
    private bool isPlayer2Turn = false;
    private Random random = new Random();
    private int playerHits = 0;
    private int playerMisses = 0;
    private int computerHits = 0;
    private int computerMisses = 0;

    // Для ручной расстановки
    private List<int> shipsToPlace = new List<int> { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
    private int currentShipIndex = 0;
    private bool currentShipHorizontal = true;
    private GameBoard placingBoard;
    private bool placingPlayer1Ships = true;

    private BotDifficulty botDifficulty = BotDifficulty.Easy;
    private List<(int x, int y)> lastHits = new();
    private (int x, int y)? lastHitDirection = null; // Направление, в котором идём
    private (int x, int y)? initialHit = null; // Первое попадание в корабль

    private Canvas placementCanvas;
    private Canvas ownCanvas;      // Всегда левое поле (своё, с кораблями)
    private Canvas enemyCanvas;    // Всегда правое поле (вражеское)

    public MainWindow()
    {
        Title = "⚓ Морской бой";
        Width = 1000;
        Height = 700;
        //Background = new SolidColorBrush(Color.FromRgb(20, 30, 50));
        Background = new ImageBrush
        {
            Source = new Bitmap(AssetLoader.Open(new Uri("avares://TestAvalonia/Assets/ShipWar.jpg"))),
            Stretch = Stretch.UniformToFill,
            Opacity = 0.6
        };

        ShowMainMenu();
    }

    private void ShowMainMenu()
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
            Text = "⚓ МОРСКОЙ БОЙ ⚓",
            FontSize = 48,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 40)
        };

        var descriptionText = new TextBlock
        {
            Text = "Game created by F.A.S.T DEVELOPMENT",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 40)
        };

        var vsComputerBtn = CreateMenuButton("🤖 Играть против компьютера", ShowDifficultyWindow);
        var vsPlayerBtn = CreateMenuButton("👥 Играть вдвоём", () => StartGame(GameMode.VsPlayer));

        menuPanel.Children.Add(titleText);
        menuPanel.Children.Add(vsComputerBtn);
        menuPanel.Children.Add(vsPlayerBtn);
        menuPanel.Children.Add(descriptionText);

        Content = menuPanel;
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
            Background = new SolidColorBrush(Color.FromRgb(20, 30, 50))
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
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
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
    private void StartWithBot(Window difficultyWindow, BotDifficulty difficulty)
    {
        botDifficulty = difficulty;
        difficultyWindow.Close();
        StartGame(GameMode.VsComputer);
    }

    private Button CreateMenuButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(40, 20),
            Background = new SolidColorBrush(Color.FromRgb(60, 90, 140)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(10),
            Width = 450,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };
        button.Click += (s, e) => onClick();
        return button;
    }

    private Button CreateDifficultyButton(string text, BotDifficulty difficulty, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            FontSize = 18,
            Padding = new Thickness(30, 15),
            Background = new SolidColorBrush(difficulty switch
            {
                BotDifficulty.Easy => Color.FromRgb(80, 120, 60),
                BotDifficulty.Medium => Color.FromRgb(120, 100, 40),
                BotDifficulty.Hard => Color.FromRgb(120, 40, 40),
                _ => Color.FromRgb(80, 120, 60)
            }),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Width = 350,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        button.Click += (s, e) => onClick();
        return button;
    }

    private void StartGame(GameMode mode)
    {
        currentMode = mode;
        playerBoard = new GameBoard();
        computerBoard = new GameBoard();

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

        string playerName = (currentMode == GameMode.VsPlayer && !placingPlayer1Ships) ? "Игрок 2" : "Игрок 1";

        var titleBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 50, 80)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 15),
            Margin = new Thickness(0, 0, 0, 20)
        };

        statusText = new TextBlock
        {
            Text = $"🚢 {playerName}: Расставьте корабли",
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        titleBorder.Child = statusText;

        var instructionText = new TextBlock
        {
            Text = currentShipIndex < shipsToPlace.Count
                ? $"Размещаем корабль размером {shipsToPlace[currentShipIndex]} клеток\nПробел - повернуть, ЛКМ - разместить"
                : "Все корабли размещены!",
            FontSize = 16,
            Foreground = Brushes.LightGray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
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
            FontSize = 16,
            Padding = new Thickness(20, 10),
            Background = new SolidColorBrush(Color.FromRgb(60, 90, 140)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        rotateBtn.Click += (s, e) => RotateCurrentShip();

        var randomBtn = new Button
        {
            Content = "🎲 Случайная расстановка",
            FontSize = 16,
            Padding = new Thickness(20, 10),
            Background = new SolidColorBrush(Color.FromRgb(80, 120, 60)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        randomBtn.Click += (s, e) => PlaceShipsRandomly();

        var startBtn = new Button
        {
            Content = "▶️ Начать игру",
            FontSize = 16,
            Padding = new Thickness(20, 10),
            Background = new SolidColorBrush(Color.FromRgb(120, 60, 60)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
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
            Width = 420,
            Height = 420,
            Background = new SolidColorBrush(Color.FromRgb(30, 50, 80))
        };

        int cellSize = 40;
        int padding = 10;

        for (int i = 0; i < placingBoard.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.LightGray
            };
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.LightGray
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
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 90, 120)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = placingBoard.Grid[x, y] == CellState.Ship
                ? new SolidColorBrush(Color.FromRgb(70, 100, 140))
                : new SolidColorBrush(Color.FromRgb(50, 80, 120)),
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        if (placingBoard.Grid[x, y] == CellState.Ship)
        {
            var content = new Canvas { Width = cellSize - 2, Height = cellSize - 2 };
            DrawShipSegment(content, cellSize - 2);
            border.Child = content;
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

        var color = highlight
            ? (canPlace ? Color.FromRgb(100, 180, 100) : Color.FromRgb(180, 100, 100))
            : (placingBoard.Grid[x, y] == CellState.Ship ? Color.FromRgb(70, 100, 140) : Color.FromRgb(50, 80, 120));

        for (int i = 0; i < shipSize; i++)
        {
            int px = currentShipHorizontal ? x + i : x;
            int py = currentShipHorizontal ? y : y + i;

            if (px >= 0 && px < placingBoard.Size && py >= 0 && py < placingBoard.Size)
            {
                var border = FindCellBorder(px, py);
                if (border != null && placingBoard.Grid[px, py] != CellState.Ship)
                {
                    border.Background = new SolidColorBrush(color);
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

    private void FinishPlacement()
    {
        KeyDown -= OnPlacementKeyDown;

        if (currentMode == GameMode.VsPlayer && placingPlayer1Ships)
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
            }
            ShowGameScreen();
        }
    }

    private void ShowGameScreen()
    {
        //if (currentMode == GameMode.VsComputer)
        //{
        //    computerBoard.PlaceShipsRandomly();
        //}
        playerTurn = true;  // ← На всякий случай
        isPlayer2Turn = false;
        ownCanvas = new Canvas { Width = 420, Height = 420, Background = new SolidColorBrush(Color.FromRgb(30, 50, 80)) };
        enemyCanvas = new Canvas { Width = 420, Height = 420, Background = new SolidColorBrush(Color.FromRgb(30, 50, 80)) };

        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        var titlePanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 50, 80)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 15),
            Margin = new Thickness(0, 0, 0, 20)
        };

        string statusMessage;
        if (currentMode == GameMode.VsPlayer)
        {
            statusMessage = isPlayer2Turn ? "⚔️ ВАШ ХОД, ИГРОК 2! Атакуйте поле противника" : "⚔️ ВАШ ХОД, ИГРОК 1! Атакуйте поле противника";
        }
        else
        {
            statusMessage = "⚔️ ВАШ ХОД! Атакуйте поле противника";
        }
        statusText = new TextBlock
        {
            Text = statusMessage,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        titlePanel.Child = statusText;

        var boardsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 50
        };

        GameBoard ownBoard = (currentMode == GameMode.VsPlayer && isPlayer2Turn) ? computerBoard : playerBoard;
        GameBoard enemyBoard = (currentMode == GameMode.VsPlayer && isPlayer2Turn) ? playerBoard : computerBoard;
        string enemyTitle = (currentMode == GameMode.VsPlayer)
            ? (isPlayer2Turn ? "🎯 ПОЛЕ ИГРОКА 1" : "🎯 ПОЛЕ ИГРОКА 2")
            : "🎯 ПОЛЕ ПРОТИВНИКА";

        var ownPanel = CreateBoardPanel(null, false, "🛡️ ВАШЕ ПОЛЕ", ownCanvas);
        var enemyPanel = CreateBoardPanel(null, true, "🎯 ПОЛЕ ПРОТИВНИКА", enemyCanvas);

        boardsPanel.Children.Add(ownPanel);
        boardsPanel.Children.Add(enemyPanel);

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
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(30, 12),
            Background = new SolidColorBrush(Color.FromRgb(60, 90, 140)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        resetButton.Click += (s, e) => ShowMainMenu();

        var menuButton = new Button
        {
            Content = "🏠 В МЕНЮ",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(30, 12),
            Background = new SolidColorBrush(Color.FromRgb(80, 60, 100)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        menuButton.Click += (s, e) => ShowMainMenu();

        buttonsPanel.Children.Add(resetButton);
        buttonsPanel.Children.Add(menuButton);

        mainPanel.Children.Add(titlePanel);
        mainPanel.Children.Add(boardsPanel);
        mainPanel.Children.Add(buttonsPanel);

        Content = mainPanel;
        mainContent = mainPanel;

        UpdateStatusAndBoards();
    }

    private Panel CreateBoardPanel(GameBoard board, bool isEnemy, string title, Canvas canvas)
    {
        var panel = new StackPanel { Spacing = 10 };

        var header = new Border
        {
            Background = new SolidColorBrush(isEnemy ? Color.FromRgb(120, 40, 40) : Color.FromRgb(40, 120, 80)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 8)
        };
        var label = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        header.Child = label;

        var statsText = new TextBlock
        {
            FontSize = 14,
            Foreground = Brushes.LightGray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };

        if (isEnemy)
        {
            computerStatsText = statsText;
            computerStatsText.Text = "💣 Статистика противника: 0 попаданий, 0 промахов";
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
            BorderBrush = new SolidColorBrush(Color.FromRgb(60, 90, 120)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = GetCellBrush(board.Grid[x, y], isEnemy),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 2,
                Blur = 4,
                Color = Color.FromArgb(100, 0, 0, 0)
            })
        };

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

            // Только если клетка доступна для выстрела
            if (board.Grid[cx, cy] == CellState.Empty || board.Grid[cx, cy] == CellState.Ship)
            {
                border.PointerPressed += (s, e) => OnGameCellClick(cx, cy);
                border.Cursor = new Cursor(StandardCursorType.Hand);

                border.PointerEntered += (s, e) =>
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(80, 110, 150));
                };

                border.PointerExited += (s, e) =>
                {
                    border.Background = GetCellBrush(board.Grid[cx, cy], isEnemy);
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

    //private async Task ComputerTurnSmart()
    //{
    //    var possibleTargets = GetSmartTargets();
    //    bool continueTurn = true;

    //    while (continueTurn && !playerTurn && possibleTargets.Count > 0)
    //    {
    //        (int x, int y) target = GetNextSmartShot(possibleTargets);
    //        possibleTargets.Remove(target);

    //        var (hit, sunk, gameOver) = playerBoard.Attack(target.x, target.y);

    //        if (hit)
    //        {
    //            computerHits++;
    //            SoundManager.PlayHit();

    //            // Запоминаем первое попадание
    //            if (initialHit == null)
    //                initialHit = target;

    //            lastHits.Add(target);
    //            if (lastHits.Count > 5) lastHits.RemoveAt(0);

    //            if (sunk)
    //            {
    //                SoundManager.PlaySunk();
    //                statusText.Text = gameOver ? "ПОРАЖЕНИЕ!" : "Противник потопил корабль!";
    //                if (gameOver) SoundManager.PlayLose();

    //                // Сбрасываем состояние
    //                initialHit = null;
    //                lastHitDirection = null;
    //                lastHits.Clear();
    //                continueTurn = false;
    //            }
    //            else
    //            {
    //                statusText.Text = "Противник попал!";
    //                // Продолжаем в том же направлении
    //            }
    //        }
    //        else
    //        {
    //            computerMisses++;
    //            SoundManager.PlayMiss();
    //            statusText.Text = "Противник промахнулся! ВАШ ХОД!";
    //            playerTurn = true;
    //            continueTurn = false;

    //            // КЛЮЧЕВАЯ ЛОГИКА: если корабль не потоплен — пробуем противоположное направление
    //            if (initialHit != null && lastHits.Count > 0)
    //            {
    //                var last = lastHits.Last();
    //                var prev = lastHits.Count > 1 ? lastHits[^2] : initialHit.Value;

    //                int dx = last.x - prev.x;
    //                int dy = last.y - prev.y;

    //                if (dx != 0 || dy != 0)
    //                {
    //                    // Пробуем противоположное направление от initialHit
    //                    int oppX = initialHit.Value.x - dx;
    //                    int oppY = initialHit.Value.y - dy;

    //                    if (IsValidAndAvailable(oppX, oppY, possibleTargets))
    //                    {
    //                        // В следующей итерации (если игрок промахнётся) — попробуем туда
    //                        lastHitDirection = (-dx, -dy);
    //                    }
    //                }
    //            }
    //        }

    //        UpdateStats();
    //        UpdateBoard(ownCanvas, playerBoard, false);

    //        if (gameOver)
    //        {
    //            continueTurn = false;
    //            playerTurn = false;
    //        }

    //        if (continueTurn && !gameOver)
    //            await Task.Delay(500);
    //    }

    //    // Сброс при окончании хода
    //    if (!continueTurn)
    //    {
    //        initialHit = null;
    //        lastHitDirection = null;
    //    }
    //}

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
            //var lastHit = lastHits.Last(); // Последнее попадание
            //var neighbors = GetNeighbors(lastHit.x, lastHit.y);

            //// Ищем доступных соседей
            //foreach (var neighbor in neighbors)
            //{
            //    if (possibleTargets.Contains(neighbor))
            //        return neighbor; // ← НАШЛИ! Стреляем рядом!
            //}
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
                foreach (var n in neighbors)
                    if (possibleTargets.Contains(n))
                        return n;
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
            if (nx >= 0 && nx < playerBoard.Size && ny >= 0 && ny < playerBoard.Size)
                neighbors.Add((nx, ny));
        }

        return neighbors;
    }

    private void UpdateStats()
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
        else
        {
            playerStatsText.Text = $"🎯 Ваши выстрелы: {playerHits} попаданий, {playerMisses} промахов";
            computerStatsText.Text = $"💣 Выстрелы противника: {computerHits} попаданий, {computerMisses} промахов";
        }
    }

    private void UpdateStatusAndBoards()
    {
        if (currentMode == GameMode.VsPlayer)
        {
            statusText.Text = isPlayer2Turn
                ? "⚔️ ВАШ ХОД, ИГРОК 2! Атакуйте поле противника"
                : "⚔️ ВАШ ХОД, ИГРОК 1! Атакуйте поле противника";
        }
        else
        {
            statusText.Text = playerTurn ? "⚔️ ВАШ ХОД! Атакуйте поле противника" : "💀 Ход противника...";
        }

        GameBoard ownBoard = (currentMode == GameMode.VsPlayer && isPlayer2Turn) ? computerBoard : playerBoard;
        GameBoard enemyBoard = (currentMode == GameMode.VsPlayer && isPlayer2Turn) ? playerBoard : computerBoard;

        string enemyTitle = currentMode == GameMode.VsPlayer
        ? (isPlayer2Turn ? "🎯 ПОЛЕ ИГРОКА 1" : "🎯 ПОЛЕ ИГРОКА 2")
        : "🎯 ПОЛЕ ПРОТИВНИКА";

        UpdateHeaderText(ownCanvas.Parent, "🛡️ ВАШЕ ПОЛЕ");
        UpdateHeaderText(enemyCanvas.Parent, enemyTitle);

        UpdateBoard(ownCanvas, ownBoard, false);
        UpdateBoard(enemyCanvas, enemyBoard, true);

        UpdateStats();
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
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.LightGray
            };
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.LightGray
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