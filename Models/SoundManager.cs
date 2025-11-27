using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BattleShipGame2.Models;

// Простой звуковой менеджер (консольный beep)
public static class SoundManager
{
    public static void PlayHit()
    {
        Task.Run(() => Console.Beep(800, 100));
    }

    public static void PlayMiss()
    {
        Task.Run(() => Console.Beep(300, 150));
    }

    public static void PlaySunk()
    {
        Task.Run(() =>
        {
            Console.Beep(600, 100);
            System.Threading.Thread.Sleep(50);
            Console.Beep(500, 100);
            System.Threading.Thread.Sleep(50);
            Console.Beep(400, 200);
        });
    }

    public static void PlayWin()
    {
        Task.Run(() =>
        {
            Console.Beep(523, 150);
            System.Threading.Thread.Sleep(50);
            Console.Beep(659, 150);
            System.Threading.Thread.Sleep(50);
            Console.Beep(784, 300);
        });
    }

    public static void PlayLose()
    {
        Task.Run(() =>
        {
            Console.Beep(400, 200);
            System.Threading.Thread.Sleep(50);
            Console.Beep(300, 300);
        });
    }
}

