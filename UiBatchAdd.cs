using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;

public static class UiBatchAdd
{
    public static async Task AddInChunksAsync<T>(
        System.Collections.ObjectModel.ObservableCollection<T> target,
        IReadOnlyList<T> items,
        int chunkSize = 500)
    {
        for (int i = 0; i < items.Count; i += chunkSize)
        {
            int end = System.Math.Min(items.Count, i + chunkSize);

            // add a chunk
            for (int j = i; j < end; j++)
                target.Add(items[j]);

            // let WPF breathe + render + process input
            await Task.Yield();
        }
    }
}