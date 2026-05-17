using WebApplication.Models.ElectronicQueueProf;

namespace WebApplication.Services.Reports.Catalog;

internal static class RouteAndPausesQueries
{
    internal static List<RouteAndPausesReportBuilder.RouteStageObservation> LoadStages(
        IQueryable<EqListItem> listItems,
        IQueryable<EqAppointment> appointments,
        DateOnly fromDo,
        DateOnly toDo) =>
        (
            from li in listItems
            join a in appointments on li.IdAppointment equals a.IdAppointment
            where a.DateArrival >= fromDo && a.DateArrival <= toDo
            select new RouteAndPausesReportBuilder.RouteStageObservation(
                a.IdAppointment,
                a.DateArrival,
                a.Info,
                a.TimeArrival,
                a.TimeComplete,
                li.TimeCall,
                li.TimeStartServicing,
                li.TimeEndServicing)).ToList();

    internal static List<RouteAndPausesReportBuilder.RouteStageObservation> LoadStages(
        IEnumerable<EqListItem> listItems,
        IEnumerable<EqAppointment> appointments,
        DateOnly fromDo,
        DateOnly toDo) =>
        LoadStages(listItems.AsQueryable(), appointments.AsQueryable(), fromDo, toDo);
}
