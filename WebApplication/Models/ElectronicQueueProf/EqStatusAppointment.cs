namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqStatusAppointment
{
    public int IdStatusApp { get; set; }
    public string Name { get; set; } = "";

    public ICollection<EqAppointment> Appointments { get; set; } = new List<EqAppointment>();
}
