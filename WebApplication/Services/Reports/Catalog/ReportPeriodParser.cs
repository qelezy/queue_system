using System.Globalization;

namespace WebApplication.Services.Reports.Catalog;

internal static class ReportPeriodParser
{
    internal static void NormalizeGenerateRequest(ReportGenerateRequest request)
    {
        var now = DateTime.UtcNow;
        var from = DateTime.TryParse(request.DateFrom, out var parsedFrom) ? parsedFrom : now.Date.AddDays(-6);
        var to = DateTime.TryParse(request.DateTo, out var parsedTo) ? parsedTo : now;
        if (from > to)
            (from, to) = (to, from);

        request.DateFrom = from.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        request.DateTo = to.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    internal static (DateTime PeriodFrom, DateTime PeriodTo, DateOnly FromDo, DateOnly ToDo) ParseCatalogPeriod(
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

    internal static (DateTime From, DateTime To) ResolveToolbarRange(string? from, string? to, DateTime today)
    {
        var toDate = TryParseDateOrDefault(to, today);
        var fromDate = TryParseDateOrDefault(from, toDate.AddDays(-6));
        if (fromDate > toDate)
            (fromDate, toDate) = (toDate, fromDate);

        return (fromDate.Date, toDate.Date);
    }

    private static DateTime TryParseDateOrDefault(string? raw, DateTime fallback) =>
        DateTime.TryParse(raw, out var parsed) ? parsed.Date : fallback.Date;
}
