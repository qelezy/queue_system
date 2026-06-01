namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqListItem
{
    public int IdListItem { get; set; }
    public int IdAppointment { get; set; }
    public int IdSpecialty { get; set; }
    public TimeOnly? TimeStartServicing { get; set; }
    public TimeOnly? TimeEndServicing { get; set; }
    public int IdStatusItem { get; set; }
    public int? IdCabinet { get; set; }
    public TimeOnly? TimeCall { get; set; }
    
    public TimeOnly? ServiceTime { get; set; }
    public int? IdDoctor { get; set; }

    public EqAppointment Appointment { get; set; } = null!;
    public EqSpecialty Specialty { get; set; } = null!;
    public EqStatusItemList StatusItem { get; set; } = null!;
    public EqCabinet Cabinet { get; set; } = null!;
    public EqDoctor Doctor { get; set; } = null!;
}
