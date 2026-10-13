using System.Collections;
using StudyConnect.Core.Enums;
using StudyConnect.Core.Matching;

namespace StudyConnect.Core.Common;

/// <summary>
/// A 7 x 3 grid (days x time blocks) describing when a student is free.
/// Implements <see cref="IEnumerable{T}"/> with a custom enumerator so it can be used
/// directly in <c>foreach</c> loops and LINQ queries, and exposes an indexer property
/// for convenient reads/writes such as <c>grid[DayOfWeek.Monday, TimeBlock.Morning] = true</c>.
/// </summary>
public sealed class WeeklyAvailability : IEnumerable<AvailabilitySlot>
{
    /// <summary>Days in the order they are shown in the UI (Monday first).</summary>
    public static readonly IReadOnlyList<DayOfWeek> OrderedDays = new[]
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    public static readonly IReadOnlyList<TimeBlock> OrderedBlocks = Enum.GetValues<TimeBlock>();

    private readonly bool[,] _grid = new bool[7, OrderedBlocks.Count];

    public WeeklyAvailability()
    {
    }

    public WeeklyAvailability(IEnumerable<AvailabilitySlot> slots)
    {
        foreach (AvailabilitySlot slot in slots)
        {
            this[slot.Day, slot.Block] = true;
        }
    }

    /// <summary>Indexer: is the student free on <paramref name="day"/> during <paramref name="block"/>?</summary>
    public bool this[DayOfWeek day, TimeBlock block]
    {
        get => _grid[(int)day, (int)block];
        set => _grid[(int)day, (int)block] = value;
    }

    /// <summary>Number of selected slots.</summary>
    public int Count => Enumerable.Count(this);

    public bool IsEmpty => Count == 0;

    public void Toggle(DayOfWeek day, TimeBlock block) => this[day, block] = !this[day, block];

    /// <summary>Custom iterator returning only the selected slots, Monday first.</summary>
    public IEnumerator<AvailabilitySlot> GetEnumerator()
    {
        foreach (DayOfWeek day in OrderedDays)
        {
            foreach (TimeBlock block in OrderedBlocks)
            {
                if (this[day, block])
                {
                    yield return new AvailabilitySlot(day, block);
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
