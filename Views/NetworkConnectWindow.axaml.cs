using Avalonia;
using Avalonia.Controls;

namespace BattleShipGame2.Views;

public partial class NetworkConnectWindow : Window
{
    public bool Success { get; private set; }
    public string Hostname { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public string PlayerName { get; private set; } = string.Empty;
    
    private TextBox _playerNameInput;
    private TextBox _serverHostInput;
    private TextBox _serverPortInput;
    private TextBlock _errorText;
    
    public NetworkConnectWindow()
    {
        InitializeComponent();
        
        // Находим элементы
        _playerNameInput = PlayerNameInput;
        _serverHostInput = ServerHostInput;
        _serverPortInput = ServerPortInput;
        var connectButton = ConnectButton;
        var backButton = NetworkBackButton;
        _errorText = ConnectionErrorTextBlock;
        
        // Обработчики событий
        connectButton.Click += OnConnectClick;
        backButton.Click += (s, e) => Close();
    }
    
    private async void OnConnectClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ValidateInput())
        {
            Success = true;
            PlayerName = _playerNameInput.Text ?? string.Empty;
            Hostname = _serverHostInput.Text ?? "127.0.0.1";
            Port = int.Parse(_serverPortInput.Text);
            Close();
        }
    }
    
    private bool ValidateInput()
    {
        _errorText.IsVisible = false;
        
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
        
        if (!int.TryParse(_serverPortInput.Text, out int portNumber) || portNumber <= 0 || portNumber > 65535)
        {
            _errorText.Text = "Неверный порт. Допустимый диапазон: 1-65535";
            _errorText.IsVisible = true;
            return false;
        }
        
        return true;
    }
}