namespace WebApplication.Models.ElectronicQueueProf;

public static class EqDateTimeExtensions
{
    
    public static DateTime CombineArrival(this EqAppointment a)
        => DateTime.SpecifyKind(a.DateArrival.ToDateTime(a.TimeArrival), DateTimeKind.Unspecified);

    public static DateTime CombineOnArrivalDate(DateOnly date, TimeOnly time)
        => DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
}
