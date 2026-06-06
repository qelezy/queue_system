using System.Globalization;
using System.Text.RegularExpressions;
using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportShared
{
    internal static bool HasAssignedDoctor(int? id) => id is > 0;

    internal static bool HasAssignedCabinet(int? id) => id is > 0;

    internal static (DateTime PeriodFrom, DateTime PeriodTo, DateOnly FromDo, DateOnly ToDo) ParsePeriod(
        ReportGenerateRequest request)
    {
        if (!DateTime.TryParse(
                request.DateFrom,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodFrom)
            || !DateTime.TryParse(
                request.DateTo,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var periodTo))
        {
            periodFrom = DateTime.UtcNow.Date.AddDays(-7);
            periodTo = DateTime.UtcNow;
        }

        if (periodFrom > periodTo)
            (periodFrom, periodTo) = (periodTo, periodFrom);

        var fromDo = DateOnly.FromDateTime(periodFrom);
        var toDo = DateOnly.FromDateTime(periodTo);
        return (periodFrom, periodTo, fromDo, toDo);
    }

    internal static void ApplyPreviewRowCap(ReportResultViewModel model, ReportGenerationPurpose purpose)
    {
        if (purpose != ReportGenerationPurpose.JsonPreview)
            return;
        if (model.Rows.Count <= ReportPreviewLimits.MaxTableRows)
            return;
        model.PreviewRowsTotal = model.Rows.Count;
        model.PreviewRowLimit = ReportPreviewLimits.MaxTableRows;
        model.Rows = model.Rows.Take(ReportPreviewLimits.MaxTableRows).ToList();
    }

    internal const int PercentDecimalPlaces = 1;

    private const int MaxDurationSeconds = 10080 * 60;

    private const string DurationSecondsSuffix = " с";

    private static readonly Regex FormattedDurationPattern = new(
        @"^(?<sign>-?)\s*(?:(?<hours>\d+)\s*ч)?\s*(?:(?<mins>\d+)\s*мин)?(?:\s*(?<secs>\d+)\s*с)?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FormattedDurationSecondsOnlyPattern = new(
        @"^(?<sign>-?)\s*(?<secs>\d+)\s*с$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryParseFormattedDurationToMinutes(string? cell, out double minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(cell) || cell.Trim() == "—")
            return false;

        var trimmed = cell.Trim();
        if (trimmed is "0" or "0 мин" or "0 сек" or "0 с")
        {
            minutes = 0;
            return true;
        }

        var secMatch = FormattedDurationSecondsOnlyPattern.Match(trimmed);
        if (secMatch.Success)
        {
            var sign = secMatch.Groups["sign"].Value == "-" ? -1 : 1;
            var secs = int.Parse(secMatch.Groups["secs"].Value, CultureInfo.InvariantCulture);
            minutes = sign * secs / 60.0;
            return true;
        }

        var match = FormattedDurationPattern.Match(trimmed);
        if (!match.Success)
            return false;

        var minSign = match.Groups["sign"].Value == "-" ? -1 : 1;
        var hours = match.Groups["hours"].Success
            ? int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture)
            : 0;
        var mins = match.Groups["mins"].Success
            ? int.Parse(match.Groups["mins"].Value, CultureInfo.InvariantCulture)
            : 0;
        var tailSecs = match.Groups["secs"].Success
            ? int.Parse(match.Groups["secs"].Value, CultureInfo.InvariantCulture)
            : 0;

        if (hours == 0 && mins == 0 && tailSecs == 0)
            return false;

        minutes = minSign * (hours * 60.0 + mins + tailSecs / 60.0);
        return true;
    }

    internal static string FormatMinutesForCsv(double minutes) =>
        Math.Round(minutes, 2, MidpointRounding.AwayFromZero)
            .ToString("F2", CultureInfo.InvariantCulture);

    internal static string FormatDurationMinutesForCsv(double? minutes) =>
        minutes is not { } value ? "" : FormatMinutesForCsv(value);

    private static double MaxAllowedDurationMinutes => MaxDurationSeconds / 60.0;

    internal static double? ComputeDurationMinutesExact(DateTime start, DateTime end)
    {
        if (end <= start)
            return null;

        var totalMinutes = (end - start).TotalMinutes;
        if (totalMinutes < 0 || totalMinutes >= MaxAllowedDurationMinutes)
            return null;

        return totalMinutes;
    }

    internal static double? ComputeDurationMinutesExact(
        DateOnly dateArrival,
        TimeOnly start,
        TimeOnly end) =>
        ComputeDurationMinutesExact(
            EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, start),
            EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, end));

    internal static double AverageDurationMinutesExact(IReadOnlyList<double> minuteValues) =>
        minuteValues.Count == 0 ? 0 : minuteValues.Average();

    internal static double MinDurationMinutesExact(IReadOnlyList<double> minuteValues) =>
        minuteValues.Count == 0 ? 0 : minuteValues.Min();

    internal static double MaxDurationMinutesExact(IReadOnlyList<double> minuteValues) =>
        minuteValues.Count == 0 ? 0 : minuteValues.Max();

    internal static int? ComputeDurationSeconds(DateTime start, DateTime end)
    {
        var seconds = (int)Math.Round((end - start).TotalSeconds, MidpointRounding.AwayFromZero);
        if (seconds < 0 || seconds >= MaxDurationSeconds)
            return null;

        return seconds;
    }

    internal static double MinutesFromSeconds(int seconds) => seconds / 60.0;

    internal static double? ComputeDurationMinutes(DateTime start, DateTime end)
    {
        var seconds = ComputeDurationSeconds(start, end);
        return seconds is null ? null : MinutesFromSeconds(seconds.Value);
    }

    internal static int RoundDurationMinutes(double minutes) =>
        (int)Math.Round(minutes, MidpointRounding.AwayFromZero);

    internal static int RoundDurationSeconds(double minutes) =>
        (int)Math.Round(minutes * 60.0, MidpointRounding.AwayFromZero);

    internal static double RoundDurationChartValue(double minutes) =>
        MinutesFromSeconds(RoundDurationSeconds(minutes));

    internal static double RoundDurationDisplayChartValue(double minutes)
    {
        var totalSeconds = RoundDurationSeconds(minutes);
        if (totalSeconds == 0)
            return 0;

        var abs = Math.Abs(totalSeconds);
        if (abs < 60)
            return MinutesFromSeconds(totalSeconds);

        var totalMinutes = (int)Math.Round(abs / 60.0, MidpointRounding.AwayFromZero);
        return totalSeconds < 0 ? -totalMinutes : totalMinutes;
    }

    internal static double RoundDurationMinutesAsDouble(double minutes) =>
        RoundDurationChartValue(minutes);

    internal static double AverageDurationMinutes(IReadOnlyList<double> minuteValues)
    {
        if (minuteValues.Count == 0)
            return 0;

        var totalSeconds = minuteValues.Sum(RoundDurationSeconds);
        var avgSeconds = (int)Math.Round(
            (double)totalSeconds / minuteValues.Count,
            MidpointRounding.AwayFromZero);
        return MinutesFromSeconds(avgSeconds);
    }

    internal static double MinDurationMinutes(IReadOnlyList<double> minuteValues) =>
        minuteValues.Count == 0
            ? 0
            : MinutesFromSeconds(minuteValues.Min(RoundDurationSeconds));

    internal static double MaxDurationMinutes(IReadOnlyList<double> minuteValues) =>
        minuteValues.Count == 0
            ? 0
            : MinutesFromSeconds(minuteValues.Max(RoundDurationSeconds));

    private static string FormatDurationSeconds(int seconds) =>
        (seconds < 0 ? "-" : "") + Math.Abs(seconds).ToString(CultureInfo.InvariantCulture) + DurationSecondsSuffix;

    internal static string FormatDuration(double minutes)
    {
        var totalSeconds = RoundDurationSeconds(minutes);
        var sign = totalSeconds < 0 ? "-" : "";
        var abs = Math.Abs(totalSeconds);

        if (abs < 60)
            return FormatDurationSeconds(totalSeconds);

        var totalMinutes = (int)Math.Round(abs / 60.0, MidpointRounding.AwayFromZero);
        if (totalMinutes < 60)
            return sign + totalMinutes.ToString(CultureInfo.InvariantCulture) + " мин";

        var hours = totalMinutes / 60;
        var remMin = totalMinutes % 60;
        if (remMin == 0)
            return sign + hours.ToString(CultureInfo.InvariantCulture) + " ч";

        return sign + hours.ToString(CultureInfo.InvariantCulture) + " ч "
            + remMin.ToString(CultureInfo.InvariantCulture) + " мин";
    }

    internal static double RoundPercent(double value) =>
        Math.Round(value, PercentDecimalPlaces);

    internal static string FormatPercent(double value)
    {
        var rounded = RoundPercent(value);
        if (rounded == 0)
            return "0";

        return rounded.ToString("0.#", CultureInfo.InvariantCulture);
    }

    internal static string FormatMultiStageSharePercent(int single, int multi)
    {
        var total = single + multi;
        return total == 0 ? "0" : FormatPercent(multi * 100.0 / total);
    }

    internal static string FormatChartDayLabel(DateOnly day) =>
        day.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
}
