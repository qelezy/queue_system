using WebApplication.Services.Reports.Catalog;

namespace WebApplication.Services.Reports.Intervals;

public static class IntervalOperations
{
    
    public static List<DateTimeInterval> MergeOverlapping(IEnumerable<DateTimeInterval> intervals)
    {
        var sorted = intervals
            .Where(i => !i.IsEmptyOrInverted)
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();
        if (sorted.Count == 0)
            return [];

        var result = new List<DateTimeInterval> { sorted[0] };
        for (var k = 1; k < sorted.Count; k++)
        {
            var cur = sorted[k];
            var last = result[^1];
            if (cur.Start <= last.End)
            {
                if (cur.End > last.End)
                    result[^1] = new DateTimeInterval(last.Start, cur.End);
            }
            else
            {
                result.Add(cur);
            }
        }

        return result;
    }

    public static DateTimeInterval? ClipToRange(DateTimeInterval interval, DateTime clipStart, DateTime clipEnd)
    {
        if (interval.IsEmptyOrInverted || clipStart >= clipEnd)
            return null;
        var s = interval.Start > clipStart ? interval.Start : clipStart;
        var e = interval.End < clipEnd ? interval.End : clipEnd;
        if (s >= e)
            return null;
        return new DateTimeInterval(s, e);
    }

    public static List<DateTimeInterval> ClipEachToRange(IEnumerable<DateTimeInterval> intervals, DateTime clipStart, DateTime clipEnd)
    {
        var list = new List<DateTimeInterval>();
        foreach (var i in intervals)
        {
            var c = ClipToRange(i, clipStart, clipEnd);
            if (c.HasValue)
                list.Add(c.Value);
        }

        return list;
    }

    public static DateTimeInterval? Intersect(DateTimeInterval a, DateTimeInterval b)
    {
        if (a.IsEmptyOrInverted || b.IsEmptyOrInverted)
            return null;
        var s = a.Start > b.Start ? a.Start : b.Start;
        var e = a.End < b.End ? a.End : b.End;
        if (s >= e)
            return null;
        return new DateTimeInterval(s, e);
    }

    public static List<DateTimeInterval> SubtractUnionFromWindow(DateTimeInterval window, IReadOnlyList<DateTimeInterval> busyIntervals)
    {
        if (window.IsEmptyOrInverted)
            return [];

        var busyClipped = MergeOverlapping(
            busyIntervals
                .Select(b => Intersect(b, window))
                .Where(x => x.HasValue)
                .Select(x => x!.Value));
        if (busyClipped.Count == 0)
            return [window];

        var idle = new List<DateTimeInterval>();
        var t = window.Start;
        foreach (var b in busyClipped)
        {
            if (b.End <= t)
                continue;
            var gapStart = t;
            var gapEnd = b.Start;
            if (gapEnd > gapStart)
                idle.Add(new DateTimeInterval(gapStart, gapEnd));
            t = b.End > t ? b.End : t;
            if (t >= window.End)
                break;
        }

        if (t < window.End)
            idle.Add(new DateTimeInterval(t, window.End));

        return idle.Where(i => !i.IsEmptyOrInverted).ToList();
    }

    public static double TotalMinutes(IEnumerable<DateTimeInterval> intervals) =>
        intervals.Sum(i =>
        {
            if (i.IsEmptyOrInverted)
                return 0;

            return CatalogReportShared.ComputeDurationMinutes(i.Start, i.End) ?? 0;
        });

    public static int CountNonEmpty(IEnumerable<DateTimeInterval> intervals) =>
        intervals.Count(i => !i.IsEmptyOrInverted);
}
