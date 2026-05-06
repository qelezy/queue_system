namespace WebApplication.Models.ElectronicQueueProf;

public static class EqDateTimeExtensions
{
    /// <summary>Склейка даты прибытия талона и времени (тот же календарный день).</summary>
    public static DateTime CombineArrival(this EqAppointment a)
        => a.DateArrival.ToDateTime(a.TimeArrival);

    public static DateTime CombineOnArrivalDate(DateOnly date, TimeOnly time)
        => date.ToDateTime(time);
}
