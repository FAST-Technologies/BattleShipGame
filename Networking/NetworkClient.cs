using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BattleShipGame.Networking;

/// <summary>
/// TCP-клиент для сетевой игры Морской Бой
/// </summary>
/// <remarks>
/// Обеспечивает подключение к игровому серверу, отправку и получение сообщений.
/// Все операции выполняются асинхронно и не блокируют UI поток.
/// </remarks>
public class NetworkClient
{
    #region Поля и свойства
    private TcpClient? _tcpClient; /// <summary>TCP клиент.</summary>
    private StreamWriter? _writer; /// <summary>Writer.</summary>
    private StreamReader? _reader; /// <summary>Reader.</summary>
    private bool _connected; /// <summary>Флаг подключения.</summary>

    /// <summary>
    /// Событие, возникающее при получении сообщения от сервера.
    /// </summary>
    public event Action<NetworkMessage>? OnMessageReceived;
    
    /// <summary>
    /// Событие, возникающее при разрыве соединения с сервером.
    /// </summary>
    public event Action? OnDisconnected;

    /// <summary>
    /// Флаг, указывающий установлено ли соединение с сервером.
    /// </summary>
    public bool IsConnected => _connected;
    #endregion
    
    #region Основная логика и работа с сообщениями
    /// <summary>
    /// Устанавливает асинхронное подключение к игровому серверу.
    /// </summary>
    /// <param name="hostname">Хост или IP-адрес сервера.</param>
    /// <param name="port">Порт сервера.</param>
    /// <returns>True если подключение установлено успешно.</returns>
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
            _ = Task.Run(ListenForMessagesAsync);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network Error] Ошибка подключения: {ex.Message}");
            _connected = false;
            return false;
        }
    }

    /// <summary>
    /// Отправляет сообщение асинхронно на сервер.
    /// </summary>
    /// <param name="message">Сообщение для отправки.</param>
    /// <returns>Задача, представляющая асинхронную операцию отправки.</returns> 
    public async Task SendMessageAsync(NetworkMessage message)
    {
        if (_writer != null && _connected)
        {
            try
            {
                var line = FormatMessage(message);
                await _writer.WriteLineAsync(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Network Error] Ошибка отправки: {ex.Message}");
                Disconnect();
            }
        }
    }

    /// <summary>
    /// Форматирует сообщение в строковый формат протокола.
    /// </summary>
    /// <param name="message">Сообщение для форматирования.</param>
    /// <returns>Строка в формате "Type:key1=value1;key2=value2".</returns>
    private string FormatMessage(NetworkMessage message)
    {
        var parts = new List<string> { message.Type };
        var dataParts = message.Data.Select(kvp => $"{kvp.Key}={kvp.Value}");
        parts.AddRange(dataParts);
        return string.Join(":", parts[0], string.Join(";", parts.Skip(1))) + "\n";
    }

    /// <summary>
    /// Асинхронно прослушивает входящие сообщения от сервера.
    /// </summary>
    /// <remarks>
    /// Выполняется в фоновом потоке. При получении сообщения вызывает событие OnMessageReceived.
    /// При разрыве соединения вызывает событие OnDisconnected.
    /// </remarks>
    private async Task ListenForMessagesAsync()
    {
        try
        {
            if (_reader == null)
            {
                Console.WriteLine("[Network Error] Reader не инициализирован");
                return;
            }
            while (_connected && await _reader.ReadLineAsync() is { } line)
            {
                var msg = ParseMessage(line);
                if (msg != null)
                    OnMessageReceived?.Invoke(msg);
            }
        }
        catch (IOException ex) when (!_connected)
        {
            Console.WriteLine($"[Network] Соединение закрыто: {ex.Message}");
        }
        catch (ObjectDisposedException) when (!_connected)
        {
            Console.WriteLine("[Network] Соединение закрыто");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network Error] Неожиданная ошибка при получении сообщения: {ex.Message}");
        }
        finally
        {
            _connected = false;
            OnDisconnected?.Invoke();
        }
    }

    /// <summary>
    /// Парсит строку сообщения в объект NetworkMessage.
    /// </summary>
    /// <param name="line">Строка сообщения в формате протокола.</param>
    /// <returns>Объект NetworkMessage или null при ошибке парсинга.</returns>
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
             Console.WriteLine($"[Network Error] Ошибка разбора сообщения: {ex.Message}");
             return null;
        }
    }
    
    #endregion

    #region Методы отключения и очистки ресурсов
    /// <summary>
    /// Отключает клиента от сервера и освобождает ресурсы.
    /// </summary>
    public void Disconnect()
    {
        _connected = false;
        try
        {
            _writer?.Close();
            _reader?.Close();
            _tcpClient?.Close();
            Console.WriteLine("[Network] Принудительное отключение от сервера");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Network] Ошибка при отключении: {ex.Message}");
        }
        finally
        {
            OnDisconnected?.Invoke();
        }
    }
    
    /// <summary>
    /// Освобождает ресурсы сетевого клиента.
    /// </summary>
    public void Dispose()
    {
        Disconnect();
        _writer?.Dispose();
        _reader?.Dispose();
        _tcpClient?.Dispose();
    }
    #endregion
}
