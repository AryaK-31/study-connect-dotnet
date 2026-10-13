namespace StudyConnect.Core.Extensions;

/// <summary>Generic collection helpers used by the matching and analytics code.</summary>
public static class SequenceExtensions
{
    /// <summary>
    /// Jaccard similarity |A ∩ B| / |A ∪ B| of two sets, between 0 (nothing in common) and 1 (identical).
    /// </summary>
    public static double JaccardSimilarity<T>(this IEnumerable<T> first, IEnumerable<T> second)
    {
        var a = first as ISet<T> ?? new HashSet<T>(first);
        var b = new HashSet<T>(second);

        if (a.Count == 0 && b.Count == 0)
        {
            return 0;
        }

        int intersection = b.Count(a.Contains);
        int union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>
    /// Returns the <paramref name="count"/> highest scoring items in descending order using a
    /// bounded min-heap (<see cref="PriorityQueue{TElement, TPriority}"/>), which is O(n log k)
    /// instead of sorting everything.
    /// </summary>
    public static IReadOnlyList<T> TopBy<T>(this IEnumerable<T> source, int count, Func<T, double> scoreSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(scoreSelector);

        if (count <= 0)
        {
            return Array.Empty<T>();
        }

        var heap = new PriorityQueue<T, double>();
        foreach (T item in source)
        {
            double score = scoreSelector(item);
            if (heap.Count < count)
            {
                heap.Enqueue(item, score);
            }
            else if (heap.TryPeek(out _, out double lowest) && score > lowest)
            {
                heap.DequeueEnqueue(item, score);
            }
        }

        var result = new List<T>(heap.Count);
        while (heap.TryDequeue(out T? item, out _))
        {
            result.Add(item);
        }

        result.Reverse();
        return result;
    }
}
