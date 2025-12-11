using System;
using NAudio.Wave;
using System.Threading.Tasks;

namespace BattleShipGame.Models2;

/// <summary>
/// Класс <c>SoundManager</c> предоставляет методы для воспроизведения звуковых эффектов в игре "Морской бой".
/// Звуки генерируются программно как синусоидальные тона и воспроизводятся через библиотеку NAudio.
/// Все методы являются статическими и потокобезопасными благодаря асинхронному воспроизведению в отдельных задачах.
/// </summary>
public static class SoundManager
{
    private static readonly int NumChannels = 1;
    /// <summary>
    /// Воспроизводит короткий высокий тон, обозначающий попадание по кораблю.
    /// </summary>
    /// <remarks>
    /// Частота: 800 Гц, Длительность: 100 мс.
    /// </remarks>
    public static void PlayHit()
    {
        PlayTone(800, 100);
    }

    /// <summary>
    /// Воспроизводит низкий тон, обозначающий промах.
    /// </summary>
    /// <remarks>
    /// Частота: 300 Гц, Длительность: 150 мс.
    /// </remarks>
    public static void PlayMiss()
    {
        PlayTone(300, 150);
    }

    /// <summary>
    /// Воспроизводит последовательность тонов, имитирующую звук потопления корабля.
    /// </summary>
    /// <remarks>
    /// Последовательность: 600 Гц → 500 Гц → 400 Гц с паузами между нотами.
    /// Выполняется асинхронно в отдельном потоке.
    /// </remarks>
    public static void PlaySunk()
    {
        Task.Run(() =>
        {
            PlayTone(600, 100);
            Task.Delay(50).Wait();
            PlayTone(500, 100);
            Task.Delay(50).Wait();
            PlayTone(400, 200);
        });
    }

    /// <summary>
    /// Воспроизводит мелодию, обозначающую победу игрока.
    /// </summary>
    /// <remarks>
    /// Ноты: C5 (523 Гц), E5 (659 Гц), G5 (784 Гц) — частичный аккорд до-мажор.
    /// Длительности: 150 мс, 150 мс, 300 мс с паузами.
    /// Выполняется асинхронно.
    /// </remarks>
    public static void PlayWin()
    {
        Task.Run(() =>
        {
            PlayTone(523, 150);
            Task.Delay(50).Wait();
            PlayTone(659, 150);
            Task.Delay(50).Wait();
            PlayTone(784, 300);
        });
    }

    /// <summary>
    /// Воспроизводит мрачную мелодию, обозначающую поражение игрока.
    /// </summary>
    /// <remarks>
    /// Последовательность: 400 Гц (200 мс) → 300 Гц (300 мс).
    /// Выполняется асинхронно.
    /// </remarks>
    public static void PlayLose()
    {
        Task.Run(() =>
        {
            PlayTone(400, 200);
            Task.Delay(50).Wait();
            PlayTone(300, 300);
        });
    }

    /// <summary>
    /// Генерирует и воспроизводит синусоидальный звук заданной частоты и длительности.
    /// </summary>
    /// <param name="frequency">Частота звука в герцах (Гц). Рекомендуемый диапазон: 100–2000 Гц.</param>
    /// <param name="durationMs">Длительность звука в миллисекундах.</param>
    /// <remarks>
    /// Метод создаёт WAV-данные в памяти, включая корректный WAV-заголовок, и воспроизводит их через NAudio.
    /// Асинхронно запускается в отдельной задаче, чтобы не блокировать UI-поток.
    /// При возникновении ошибки (например, отсутствие аудиоустройства) — ошибка логируется в консоль.
    /// </remarks>
    /// <exception cref="ArgumentException">Выбрасывается, если частота ≤ 0 или длительность ≤ 0 (не в текущей реализации, но логично для расширения).</exception>
    private static void PlayTone(double frequency, int durationMs)
    {
        Task.Run(() =>
        {
            try
            {
                int sampleRate = 44100;
                double amplitude = 0.5;

                // Создаем WAV файл в памяти
                using var memoryStream = new System.IO.MemoryStream();

                // Заголовок WAV файла
                WriteWavHeader(memoryStream, durationMs, sampleRate);

                // Аудиоданные (синусоида)
                short[] data = new short[(int)(sampleRate * durationMs / 1000.0)];
                double freq = frequency * 2.0 * Math.PI / sampleRate;

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (short)(amplitude * Math.Sin(freq * i) * short.MaxValue);
                }

                // Конвертируем short[] в byte[]
                byte[] byteData = new byte[data.Length * 2];
                Buffer.BlockCopy(data, 0, byteData, 0, byteData.Length);

                memoryStream.Write(byteData, 0, byteData.Length);
                memoryStream.Position = 0;
                
                using var audioFile = new WaveFileReader(memoryStream);
                using var outputDevice = new WaveOutEvent();

                outputDevice.Init(audioFile);
                outputDevice.Play();
                
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    Task.Delay(50).Wait();
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sound Error] {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Записывает корректный заголовок WAV-файла в указанный поток.
    /// </summary>
    /// <param name="stream">Поток, в который записывается заголовок.</param>
    /// <param name="durationMs">Длительность аудио в миллисекундах.</param>
    /// <param name="sampleRate">Частота дискретизации (Гц). По умолчанию 44100.</param>
    /// <remarks>
    /// Формат: PCM, моно, 16 бит на выборку.
    /// </remarks>
    private static void WriteWavHeader(System.IO.Stream stream, int durationMs, int sampleRate)
    {
        int bitsPerSample = 16;
        int numSamples = sampleRate * durationMs / 1000;
        int subChunk2Size = numSamples * NumChannels * bitsPerSample / 8;

        // RIFF заголовок
        WriteString(stream, "RIFF");
        WriteInt(stream, 36 + subChunk2Size);
        WriteString(stream, "WAVE");

        // fmt подзаголовок
        WriteString(stream, "fmt ");
        WriteInt(stream, 16);
        WriteShort(stream, 1); // AudioFormat = PCM
        WriteShort(stream, (short)NumChannels);
        WriteInt(stream, sampleRate);
        WriteInt(stream, sampleRate * NumChannels * bitsPerSample / 8); // ByteRate
        WriteShort(stream, (short)(NumChannels * bitsPerSample / 8)); // BlockAlign
        WriteShort(stream, (short)bitsPerSample);

        // data подзаголовок
        WriteString(stream, "data");
        WriteInt(stream, subChunk2Size);
    }

    private static void WriteString(System.IO.Stream stream, string s)
    {
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteInt(System.IO.Stream stream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteShort(System.IO.Stream stream, short value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }
}