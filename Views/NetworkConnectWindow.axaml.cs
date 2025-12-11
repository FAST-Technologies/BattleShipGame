using Avalonia.Controls;

namespace BattleShipGame.Views;

/// <summary>
/// Окно подключения к сетевой игре.
/// Позволяет пользователю ввести имя, адрес сервера и порт для подключения.
/// </summary>
public partial class NetworkConnectWindow : Window
{
    /// <summary>
    /// Получает флаг успешного подключения.
    /// </summary>
    public bool Success { get; private set; }
    
    /// <summary>
    /// Получает введенное имя хоста сервера.
    /// </summary>
    public string Hostname { get; private set; } = string.Empty;
    
    /// <summary>
    /// Получает введенный порт сервера.
    /// </summary>
    public int Port { get; private set; }
    
    /// <summary>
    /// Получает введенное имя игрока.
    /// </summary>
    public string PlayerName { get; private set; } = string.Empty;
    
    private readonly TextBox _playerNameInput;
    private readonly TextBox _serverHostInput;
    private readonly TextBox _serverPortInput;
    private readonly TextBlock _errorText;
    
    /// <summary>
    /// Инициализирует новый экземпляр класса NetworkConnectWindow.
    /// Настраивает обработчики событий для кнопок подключения и возврата.
    /// </summary>
    public NetworkConnectWindow()
    {
        InitializeComponent();
        
        _playerNameInput = PlayerNameInput;
        _serverHostInput = ServerHostInput;
        _serverPortInput = ServerPortInput;
        var connectButton = ConnectButton;
        var backButton = NetworkBackButton;
        _errorText = ConnectionErrorTextBlock;
        
        // Обработчики событий
        connectButton.Click += OnConnectClick;
        backButton.Click += (_, _) => Close();
    }
    
    /// <summary>
    /// Обработчик клика по кнопке "Подключиться".
    /// Проверяет введенные данные и закрывает окно с результатом.
    /// </summary>
    /// <param name="sender">Источник события.</param>
    /// <param name="e">Аргументы события маршрутизации.</param>
    private void OnConnectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ValidateInput(out int portNumber))
        {
            Success = true;
            PlayerName = _playerNameInput.Text ?? string.Empty;
            Hostname = _serverHostInput.Text ?? "127.0.0.1";
            Port = portNumber;
            Close();
        }
    }
    
    /// <summary>
    /// Проверяет корректность введенных пользователем данных.
    /// </summary>
    /// <param name="port">Выходной параметр для распарсенного порта.</param>
    /// <returns>true, если данные корректны; иначе false.</returns>
    private bool ValidateInput(out int port)
    {
        _errorText.IsVisible = false;
        port = 0; // Инициализация по умолчанию
    
        if (string.IsNullOrWhiteSpace(_playerNameInput.Text))
        {
            _errorText.Text = "Введите имя игрока";
            _errorText.IsVisible = true;
            return false;
        }
    
        if (string.IsNullOrWhiteSpace(_serverHostInput.Text))
        {
            _errorText.Text = "Введите адрес сервера";
            _errorText.IsVisible = true;
            return false;
        }
    
        if (!int.TryParse(_serverPortInput.Text, out port) || port <= 0 || port > 65535)
        {
            _errorText.Text = "Неверный порт. Допустимый диапазон: 1-65535";
            _errorText.IsVisible = true;
            return false;
        }
    
        return true;
    }
}