namespace WebApplication.Models.ElectronicQueueProf;

/// <summary>
/// Строка списка очереди по талону. Дата событий по умолчанию — <see cref="EqAppointment.DateArrival"/> родительского талона.
/// Поле <see cref="ServiceTime"/> в БД имеет тип time и по данным трактуется как длительность (как TimeSpan от полуночи).
/// </summary>
public sealed class EqListItem
{
    public int IdListItem { get; set; }
    public int IdAppointment { get; set; }
    public int IdSpecialty { get; set; }
    public TimeOnly? TimeStartServicing { get; set; }
    public TimeOnly? TimeEndServicing { get; set; }
    public int IdStatusItem { get; set; }
    public int IdCabinet { get; set; }
    public TimeOnly? TimeCall { get; set; }
    /// <summary>Длительность, закодированная как time в SQL Server.</summary>
    public TimeOnly? ServiceTime { get; set; }
    public int IdDoctor { get; set; }

    public EqAppointment Appointment { get; set; } = null!;
    public EqSpecialty Specialty { get; set; } = null!;
    public EqStatusItemList StatusItem { get; set; } = null!;
    public EqCabinet Cabinet { get; set; } = null!;
    public EqDoctor Doctor { get; set; } = null!;
}
