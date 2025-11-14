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
    private bool playerTurn = true;
    private Random random = new Random();

    private void InitializeGame()
    {
        playerBoard = new GameBoard();
        computerBoard = new GameBoard();

        playerBoard.PlaceShipsRandomly();
        computerBoard.PlaceShipsRandomly();
    }

    private void BuildUI()
    {
        var mainPanel = new StackPanel
        {
            Margin = new Thickness(20)
        };

        // Заголовок
        statusText = new TextBlock
        {
            Text = "⚔️ ВАШ ХОД! Атакуйте поле противника",
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20)
        };

        var boardsPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 50
        };

        // Панель игрока
        var playerPanel = new StackPanel { Spacing = 10 };

        var playerLabel = new TextBlock
        {
            Text = "🛡️ ВАШЕ ПОЛЕ",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        playerCanvas = CreateBoardCanvas(playerBoard, false);

        playerPanel.Children.Add(playerLabel);
        playerPanel.Children.Add(playerCanvas);

        // Панель компьютера
        var computerPanel = new StackPanel { Spacing = 10 };

        var computerLabel = new TextBlock
        {
            Text = "🎯 ПОЛЕ ПРОТИВНИКА",
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        computerCanvas = CreateBoardCanvas(computerBoard, true);

        computerPanel.Children.Add(computerLabel);
        computerPanel.Children.Add(computerCanvas);

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
            Foreground = Brushes.White
        };
        resetButton.Click += (s, e) => ResetGame();

        mainPanel.Children.Add(statusText);
        mainPanel.Children.Add(boardsPanel);
        mainPanel.Children.Add(resetButton);

        Content = mainPanel;
    }

    private Canvas CreateBoardCanvas(GameBoard board, bool isEnemy)
    {
        var canvas = new Canvas
        {
            Width = 400,
            Height = 400,
            Background = new SolidColorBrush(Color.FromRgb(30, 50, 80))
        };

        int cellSize = 40;

        // Отрисовка клеток
        for (int i = 0; i < board.Size; i++)
        {
            for (int j = 0; j < board.Size; j++)
            {
                var cell = CreateCell(board, i, j, cellSize, isEnemy);
                Canvas.SetLeft(cell, i * cellSize);
                Canvas.SetTop(cell, j * cellSize);
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
            Background = GetCellBrush(board.Grid[x, y], isEnemy)
        };

        var content = new Canvas { Width = cellSize - 2, Height = cellSize - 2 };

        // Добавляем визуальные элементы в зависимости от состояния
        if (board.Grid[x, y] == CellState.Miss)
        {
            DrawMiss(content, cellSize - 2);
        }
        else if (board.Grid[x, y] == CellState.Hit || board.Grid[x, y] == CellState.Sunk)
        {
            DrawHit(content, cellSize - 2);
        }

        border.Child = content;

        if (isEnemy)
        {
            int cx = x, cy = y;
            border.PointerPressed += (s, e) => OnCellClick(cx, cy);
            border.Cursor = new Cursor(StandardCursorType.Hand);
        }

        return border;
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
            Stroke = Brushes.Red,
            StrokeThickness = 3
        };
        var line2 = new Line
        {
            StartPoint = new Point(size * 0.8, size * 0.2),
            EndPoint = new Point(size * 0.2, size * 0.8),
            Stroke = Brushes.Red,
            StrokeThickness = 3
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
            if (gameOver)
            {
                statusText.Text = "🎉 ПОБЕДА! Вы потопили весь флот противника!";
                playerTurn = false;
            }
            else
            {
                statusText.Text = "🔥 ПОПАДАНИЕ! Атакуйте снова!";
            }
            UpdateBoard(computerCanvas, computerBoard, true);
        }
        else
        {
            statusText.Text = "💧 Промах! Ход переходит к противнику...";
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

        while (!playerTurn && possibleTargets.Count > 0)
        {
            int randomIndex = random.Next(possibleTargets.Count);
            var (x, y) = possibleTargets[randomIndex];
            possibleTargets.RemoveAt(randomIndex);

            var (hit, sunk, gameOver) = playerBoard.Attack(x, y);

            if (hit)
            {
                if (gameOver)
                {
                    statusText.Text = "💀 ПОРАЖЕНИЕ! Противник уничтожил ваш флот!";
                    playerTurn = false;
                }
                UpdateBoard(playerCanvas, playerBoard, false);
            }
            else
            {
                statusText.Text = "⚔️ Противник промахнулся! ВАШ ХОД!";
                playerTurn = true;
                UpdateBoard(playerCanvas, playerBoard, false);
                break;
            }

            if (gameOver) break;

            await Task.Delay(500);
        }
    }

    private void UpdateBoard(Canvas canvas, GameBoard board, bool isEnemy)
    {
        canvas.Children.Clear();

        int cellSize = 40;

        for (int i = 0; i < board.Size; i++)
        {
            for (int j = 0; j < board.Size; j++)
            {
                var cell = CreateCell(board, i, j, cellSize, isEnemy);
                Canvas.SetLeft(cell, i * cellSize);
                Canvas.SetTop(cell, j * cellSize);
                canvas.Children.Add(cell);
            }
        }
    }

    private void ResetGame()
    {
        InitializeGame();
        playerTurn = true;
        statusText.Text = "⚔️ ВАШ ХОД! Атакуйте поле противника";
        UpdateBoard(playerCanvas, playerBoard, false);
        UpdateBoard(computerCanvas, computerBoard, true);
    }
}