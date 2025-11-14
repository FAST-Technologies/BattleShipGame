using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia;
using BattleShipGame.Models;
using Avalonia.Controls.Shapes;

namespace BattleShipGame2.Views;

public partial class MainWindow : Window
{
    private GameBoard playerBoard;
    private GameBoard computerBoard;
    private Canvas playerCanvas;
    private Canvas computerCanvas;
    private TextBlock statusText;
    private TextBlock playerStatsText;
    private TextBlock computerStatsText;
    private bool playerTurn = true;
    private Random random = new Random();
    private int playerHits = 0;
    private int playerMisses = 0;
    private int computerHits = 0;
    private int computerMisses = 0;

    public MainWindow()
    {
        Title = "⚓ Морской бой";
        Width = 1000;
        Height = 700;
        Background = new SolidColorBrush(Color.FromRgb(20, 30, 50));

        InitializeGame();
        BuildUI();
    }

    private void InitializeGame()
    {
        playerBoard = new GameBoard();
        computerBoard = new GameBoard();

        playerBoard.PlaceShipsRandomly();
        computerBoard.PlaceShipsRandomly();

        playerHits = 0;
        playerMisses = 0;
        computerHits = 0;
        computerMisses = 0;
    }

    private void BuildUI()
    {
        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        // Заголовок
        var titlePanel = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 50, 80)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 15),
            Margin = new Thickness(0, 0, 0, 20)
        };

        statusText = new TextBlock
        {
            Text = "⚔️ ВАШ ХОД! Атакуйте поле противника",
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

        // Панель игрока
        var playerPanel = new StackPanel { Spacing = 10 };
        
        var playerHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 120, 80)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 8)
        };
        var playerLabel = new TextBlock
        {
            Text = "🛡️ ВАШЕ ПОЛЕ",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        playerHeader.Child = playerLabel;

        playerCanvas = CreateBoardCanvas(playerBoard, false);
        
        playerStatsText = new TextBlock
        {
            Text = "🎯 Статистика: 0 попаданий, 0 промахов",
            FontSize = 14,
            Foreground = Brushes.LightGray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };

        playerPanel.Children.Add(playerHeader);
        playerPanel.Children.Add(playerCanvas);
        playerPanel.Children.Add(playerStatsText);

        // Панель компьютера
        var computerPanel = new StackPanel { Spacing = 10 };
        
        var computerHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(120, 40, 40)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15, 8)
        };
        var computerLabel = new TextBlock
        {
            Text = "🎯 ПОЛЕ ПРОТИВНИКА",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        computerHeader.Child = computerLabel;

        computerCanvas = CreateBoardCanvas(computerBoard, true);
        
        computerStatsText = new TextBlock
        {
            Text = "💣 Статистика противника: 0 попаданий, 0 промахов",
            FontSize = 14,
            Foreground = Brushes.LightGray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };

        computerPanel.Children.Add(computerHeader);
        computerPanel.Children.Add(computerCanvas);
        computerPanel.Children.Add(computerStatsText);

        boardsPanel.Children.Add(playerPanel);
        boardsPanel.Children.Add(computerPanel);

        // Кнопка новой игры
        var resetButton = new Button
        {
            Content = "🔄 НОВАЯ ИГРА",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(30, 12),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(60, 90, 140)),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        resetButton.Click += (s, e) => ResetGame();

        mainPanel.Children.Add(titlePanel);
        mainPanel.Children.Add(boardsPanel);
        mainPanel.Children.Add(resetButton);

        Content = mainPanel;
    }

    private Canvas CreateBoardCanvas(GameBoard board, bool isEnemy)
    {
        var canvas = new Canvas
        {
            Width = 420,
            Height = 420,
            Background = new SolidColorBrush(Color.FromRgb(30, 50, 80))
        };

        int cellSize = 40;
        int padding = 10;

        // Координаты
        for (int i = 0; i < board.Size; i++)
        {
            var letterText = new TextBlock
            {
                Text = ((char)('А' + i)).ToString(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Canvas.SetLeft(letterText, padding + i * cellSize + cellSize / 2 - 5);
            Canvas.SetTop(letterText, 0);
            canvas.Children.Add(letterText);

            var numberText = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.LightGray,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Canvas.SetLeft(numberText, 0);
            Canvas.SetTop(numberText, padding + i * cellSize + cellSize / 2 - 7);
            canvas.Children.Add(numberText);
        }

        // Отрисовка клеток
        for (int i = 0; i < board.Size; i++)
        {
            for (int j = 0; j < board.Size; j++)
            {
                var cell = CreateCell(board, i, j, cellSize, isEnemy);
                Canvas.SetLeft(cell, padding + i * cellSize);
                Canvas.SetTop(cell, padding + j * cellSize);
                canvas.Children.Add(cell);
            }
        }

        return canvas;
    }

    private Control CreateCell(GameBoard board, int x, int y, int cellSize, bool isEnemy)
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

        border.Child = content;

        if (isEnemy)
        {
            int cx = x, cy = y;
            border.PointerPressed += (s, e) => OnCellClick(cx, cy);
            border.Cursor = new Cursor(StandardCursorType.Hand);
            
            border.PointerEntered += (s, e) =>
            {
                if (board.Grid[cx, cy] == CellState.Empty || board.Grid[cx, cy] == CellState.Ship)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(80, 110, 150));
                }
            };
            
            border.PointerExited += (s, e) =>
            {
                border.Background = GetCellBrush(board.Grid[cx, cy], isEnemy);
            };
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
            _ => new SolidColorBrush(Color.FromRgb(50, 80, 120))
        };
    }

    private async void OnCellClick(int x, int y)
    {
        if (!playerTurn) return;

        var (hit, sunk, gameOver) = computerBoard.Attack(x, y);

        if (hit)
        {
            playerHits++;
            if (sunk)
            {
                statusText.Text = gameOver 
                    ? "🎉 ПОБЕДА! Вы потопили весь флот противника!" 
                    : "💥 Корабль потоплен! Продолжайте атаку!";
            }
            else
            {
                statusText.Text = "🔥 ПОПАДАНИЕ! Атакуйте снова!";
            }

            if (gameOver)
            {
                playerTurn = false;
            }
            UpdateStats();
            UpdateBoard(computerCanvas, computerBoard, true);
            
            if (!gameOver)
            {
                return;
            }
        }
        else if (computerBoard.Grid[x, y] == CellState.Miss)
        {
            playerMisses++;
            statusText.Text = "💧 Промах! Ход переходит к противнику...";
            UpdateStats();
            UpdateBoard(computerCanvas, computerBoard, true);
            playerTurn = false;
            await Task.Delay(800);
            
            await ComputerTurn();
        }
    }

    private async Task ComputerTurn()
    {
        var possibleTargets = new List<(int x, int y)>();
        for (int x = 0; x < playerBoard.Size; x++)
        {
            for (int y = 0; y < playerBoard.Size; y++)
            {
                possibleTargets.Add((x, y));
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
                if (sunk)
                {
                    statusText.Text = gameOver
                        ? "💀 ПОРАЖЕНИЕ! Противник уничтожил ваш флот!"
                        : "⚠️ Противник потопил ваш корабль!";

                    if (gameOver)
                    {
                        continueTurn = false;
                    }
                }
                else
                {
                    statusText.Text = "💥 Противник попал в ваш корабль!";
                }
                UpdateStats();
                UpdateBoard(playerCanvas, playerBoard, false);
            }
            else
            {
                computerMisses++;
                statusText.Text = "⚔️ Противник промахнулся! ВАШ ХОД!";
                playerTurn = true;
                continueTurn = false;
                UpdateStats();
                UpdateBoard(playerCanvas, playerBoard, false);
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
        
        if (possibleTargets.Count == 0)
        {
            statusText.Text = "⚠️ Все клетки были атакованы. Проверьте состояние игры.";
            playerTurn = true;
            continueTurn = false;
        }
    }

    private void UpdateStats()
    {
        playerStatsText.Text = $"🎯 Ваши выстрелы: {playerHits} попаданий, {playerMisses} промахов";
        computerStatsText.Text = $"💣 Выстрелы противника: {computerHits} попаданий, {computerMisses} промахов";
    }

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
                var cell = CreateCell(board, i, j, cellSize, isEnemy);
                Canvas.SetLeft(cell, padding + i * cellSize);
                Canvas.SetTop(cell, padding + j * cellSize);
                canvas.Children.Add(cell);
            }
        }
    }

    private void ResetGame()
    {
        InitializeGame();
        playerTurn = true;
        statusText.Text = "⚔️ ВАШ ХОД! Атакуйте поле противника";
        UpdateStats();
        UpdateBoard(playerCanvas, playerBoard, false);
        UpdateBoard(computerCanvas, computerBoard, true);
    }
}