namespace StudyConnect.Core.Common;

/// <summary>
/// Combinatorics helpers used by the team builder.
/// </summary>
public static class Combinatorics
{
    /// <summary>
    /// Lazily enumerates every combination of <paramref name="size"/> items from
    /// <paramref name="items"/> in lexicographic index order, using a custom iterator
    /// (<c>yield return</c>) so that combinations are produced one at a time instead of
    /// being materialised in memory.
    /// </summary>
    /// <example>Combinations([A,B,C], 2) yields [A,B], [A,C], [B,C].</example>
    public static IEnumerable<IReadOnlyList<T>> Combinations<T>(IReadOnlyList<T> items, int size)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (size < 0 || size > items.Count)
        {
            yield break;
        }

        int[] indices = Enumerable.Range(0, size).ToArray();

        while (true)
        {
            yield return indices.Select(i => items[i]).ToArray();

            // Find the right-most index that can still be incremented.
            int position = size - 1;
            while (position >= 0 && indices[position] == items.Count - size + position)
            {
                position--;
            }

            if (position < 0)
            {
                yield break;
            }

            indices[position]++;
            for (int next = position + 1; next < size; next++)
            {
                indices[next] = indices[next - 1] + 1;
            }
        }
    }

    /// <summary>Number of combinations "n choose k" (used in tests and UI hints).</summary>
    public static long Choose(int n, int k)
    {
        if (k < 0 || k > n)
        {
            return 0;
        }

        k = Math.Min(k, n - k);
        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            result = result * (n - k + i) / i;
        }

        return result;
    }
}
