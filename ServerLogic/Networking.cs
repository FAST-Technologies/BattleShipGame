namespace BattleShipGame2.Networking
{
    public class NetworkMessage
    {
        public string Type { get; set; }
        public Dictionary<string, string> Data { get; set; } = new();
    }

    public class NetworkClient
    {
        private TcpClient? _tcpClient;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private bool _connected = false;
        private Task? _listenTask;

        public event Action<NetworkMessage>? OnMessageReceived;
        public event Action? OnDisconnected;

        public bool IsConnected => _connected;

        public async Task<bool> ConnectAsync(string hostname, int port)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(hostname, port);
                var stream = _tcpClient.GetStream();
                _writer = new StreamWriter(stream) { AutoFlush = true };
                _reader = new StreamReader(stream);
                _connected = true;

                _listenTask = Task.Run(ListenForMessagesAsync);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                _connected = false;
                return false;
            }
        }

        public async Task SendMessageAsync(NetworkMessage message)
        {
            if (_writer != null && _connected)
            {
                var line = FormatMessage(message);
                await _writer.WriteLineAsync(line);
            }
        }

        private string FormatMessage(NetworkMessage message)
        {
            var parts = new List<string> { message.Type };
            var dataParts = message.Data.Select(kvp => $"{kvp.Key}={kvp.Value}");
            parts.AddRange(dataParts);
            return string.Join(":", parts[0], string.Join(";", parts.Skip(1))) + "\n";
        }

        private async Task ListenForMessagesAsync()
        {
            try
            {
                string? line;
                while (_connected && (line = await _reader.ReadLineAsync()) != null)
                {
                    var msg = ParseMessage(line);
                    if (msg != null)
                        OnMessageReceived?.Invoke(msg);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении сообщения: {ex.Message}");
            }
            finally
            {
                _connected = false;
                OnDisconnected?.Invoke();
            }
        }

        private NetworkMessage? ParseMessage(string line)
        {
            try
            {
                 var parts = line.Split(':', 2);
                 if (parts.Length < 2) return null;

                 var message = new NetworkMessage { Type = parts[0] };
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
            catch (Exception ex)
            {
                 Console.WriteLine($"Ошибка разбора сообщения: {ex.Message}");
                 return null;
            }
        }

        public void Disconnect()
        {
            _connected = false;
            _listenTask?.Wait(); // Дождаться завершения прослушивания
            _writer?.Close();
            _reader?.Close();
            _tcpClient?.Close();
        }
    }
}