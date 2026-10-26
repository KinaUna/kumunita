using Kumunita.Core.Query;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Pure tests for <see cref="WeekAnchor"/> (ADR 0169) — the one shared
/// first-day-of-week resolution + day-ordering helper. No DB, no actor:
/// <see cref="WeekAnchor.Resolve"/> normalizes a resident's preference (or
/// unset) to a concrete <c>System.DayOfWeek</c> ordinal, and
/// <see cref="WeekAnchor.OrderDays"/> reorders the 0…6 day set to lead with
/// the preferred first day. These pin the Monday-default floor (a
/// <c>null</c> / out-of-range preference resolves to Monday = 1, so the
/// default calendar / pickers are byte-identical to the pre-preference
/// ISO Monday-start shape) and the wrap-around ordering every day-of-week
/// selector relies on.
/// </summary>
public sealed class WeekAnchorTests
{
    // ── Resolve: the floor + range ────────────────────────────────────────

    [Theory]
    [InlineData(null, 1)]   // unset → the Monday default
    [InlineData(1, 1)]      // explicit Monday → Monday
    [InlineData(0, 0)]      // explicit Sunday → Sunday (the opt-in)
    [InlineData(6, 6)]      // explicit Saturday → Saturday
    public void Resolve_NullOrInRange_ReturnsTheExpectedOrdinal(int? preferred, int expected)
    {
        Assert.Equal(expected, WeekAnchor.Resolve(preferred));
    }

    [Theory]
    [InlineData(7)]         // above range → the Monday default
    [InlineData(8)]
    [InlineData(-1)]        // below range → the Monday default
    [InlineData(-3)]
    [InlineData(100)]
    public void Resolve_OutOfRange_FallsBackToTheMondayDefault(int outOfRange)
    {
        Assert.Equal(WeekAnchor.Default, WeekAnchor.Resolve(outOfRange));
    }

    [Fact]
    public void Default_Is_Monday()
    {
        Assert.Equal(1, WeekAnchor.Default);
        Assert.Equal(System.DayOfWeek.Monday, (System.DayOfWeek)WeekAnchor.Default);
    }

    // ── OrderDays: the wrap-around reorder ────────────────────────────────

    [Fact]
    public void OrderDays_MondayFirst()
    {
        // firstDay = 1 (Monday) → Monday…Sunday.
        Assert.Equal(new int[] { 1, 2, 3, 4, 5, 6, 0 }, WeekAnchor.OrderDays(1));
    }

    [Fact]
    public void OrderDays_SundayFirst()
    {
        // firstDay = 0 (Sunday) → Sunday…Saturday (the natural ordinal order).
        Assert.Equal(new int[] { 0, 1, 2, 3, 4, 5, 6 }, WeekAnchor.OrderDays(0));
    }

    [Fact]
    public void OrderDays_WrapsFromAnyStartDay()
    {
        // firstDay = 3 (Wednesday) → Wednesday, Thursday, Friday, Saturday,
        // Sunday, Monday, Tuesday.
        Assert.Equal(new int[] { 3, 4, 5, 6, 0, 1, 2 }, WeekAnchor.OrderDays(3));

        // firstDay = 5 (Friday) → Friday, Saturday, Sunday, Monday, Tuesday,
        // Wednesday, Thursday.
        Assert.Equal(new int[] { 5, 6, 0, 1, 2, 3, 4 }, WeekAnchor.OrderDays(5));
    }

    [Fact]
    public void OrderDays_AlwaysSevenDistinctDays()
    {
        for (var first = 0; first <= 6; first++)
        {
            var ordered = WeekAnchor.OrderDays(first);
            Assert.Equal(7, ordered.Length);
            Assert.Equal(7, ordered.Distinct().Count());
            // Covers exactly the 0…6 set (a permutation, not a subset).
            Assert.Equal(new int[] { 0, 1, 2, 3, 4, 5, 6 }, ordered.OrderBy(n => n).ToArray());
            // The first element is always the requested first day.
            Assert.Equal(first, ordered[0]);
        }
    }

    [Fact]
    public void OrderDays_ResolvedDefaultMatchesMondayFirst()
    {
        // A resident who has not set a preference resolves to the Monday
        // default, so the picker order is the Monday-first one.
        Assert.Equal(WeekAnchor.OrderDays(1), WeekAnchor.OrderDays(WeekAnchor.Resolve(null)));
    }
}
