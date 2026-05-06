namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqCategory
{
    public int IdCategory { get; set; }
    public string Name { get; set; } = "";
    public int Priority { get; set; }

    public ICollection<EqAppointment> Appointments { get; set; } = new List<EqAppointment>();
}
