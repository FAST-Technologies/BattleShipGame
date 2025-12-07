/*using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace BattleShipGame2.Views;

public static class WindowExtensions
{
    public static async Task<TResult?> ShowDialog<TResult>(this Window window, Window owner)
    {
        var tcs = new TaskCompletionSource<TResult?>();
        
        window.Closed += (s, e) =>
        {
            if (window.DataContext is TResult result)
            {
                tcs.SetResult(result);
            }
            else
            {
                tcs.SetResult(default);
            }
        };
        
        await window.ShowDialog(owner);
        return await tcs.Task;
    }
}*/