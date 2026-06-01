using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class CatalogReportWaitingHelper
{
    internal readonly record struct WaitStageRow(
        int IdListItem,
        int IdAppointment,
        DateOnly DateArrival,
        TimeOnly TimeArrival,
        TimeOnly? TimeCall,
        TimeOnly? TimeStartServicing,
        TimeOnly? TimeEndServicing) : IWaitStageRow;

    internal static List<T> OrderStagesForAppointment<T>(IEnumerable<T> stages)
        where T : IWaitStageRow =>
        stages
            .OrderBy(x => x.TimeStartServicing ?? TimeOnly.MaxValue)
            .ThenBy(x => x.IdListItem)
            .ToList();

    internal static double? TryComputeWaitBeforeCallMinutes<T>(
        DateOnly dateArrival,
        TimeOnly timeArrival,
        IReadOnlyList<T> orderedStages,
        int stageIndex,
        TimeOnly timeCall)
        where T : IWaitStageRow
    {
        if (stageIndex < 0 || stageIndex >= orderedStages.Count)
            return null;

        DateTime waitStart;
        if (stageIndex == 0)
        {
            waitStart = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeArrival);
        }
        else
        {
            var previous = orderedStages[stageIndex - 1];
            if (previous.TimeEndServicing is { } prevEnd)
            {
                waitStart = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, prevEnd);
            }
            else if (previous.TimeStartServicing is { } prevStart)
            {
                waitStart = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, prevStart);
            }
            else
            {
                return null;
            }
        }

        var callDt = EqDateTimeExtensions.CombineOnArrivalDate(dateArrival, timeCall);
        var waitMin = (callDt - waitStart).TotalMinutes;
        return waitMin >= 0 && waitMin < 10080 ? waitMin : null;
    }

    internal static List<WaitingBeforeAppointmentReportBuilder.WaitingObservation> BuildWaitingObservations(
        IEnumerable<WaitStageRow> rows,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var observations = new List<WaitingBeforeAppointmentReportBuilder.WaitingObservation>();
        foreach (var appointmentGroup in rows.GroupBy(x => x.IdAppointment))
        {
            var ordered = OrderStagesForAppointment(appointmentGroup);
            for (var i = 0; i < ordered.Count; i++)
            {
                var stage = ordered[i];
                if (stage.TimeCall is not { } timeCall)
                    continue;

                if (!WaitingBeforeAppointmentReportBuilder.IsCallInPeriod(
                        stage.DateArrival, timeCall, periodFrom, periodTo))
                    continue;

                var waitMin = TryComputeWaitBeforeCallMinutes(
                    stage.DateArrival,
                    stage.TimeArrival,
                    ordered,
                    i,
                    timeCall);
                if (waitMin is null)
                    continue;

                observations.Add(new WaitingBeforeAppointmentReportBuilder.WaitingObservation(
                    stage.DateArrival,
                    stage.TimeArrival.Hour,
                    waitMin.Value));
            }
        }

        return observations;
    }

    internal interface IWaitStageRow
    {
        int IdListItem { get; }
        TimeOnly? TimeCall { get; }
        TimeOnly? TimeStartServicing { get; }
        TimeOnly? TimeEndServicing { get; }
    }
}
