namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqCategory
{
    public int IdCategory { get; set; }
    public int IdSetting { get; set; }
    public string Name { get; set; } = "";
    public int Priority { get; set; }

    public EqSettingQueue Setting { get; set; } = null!;
    public ICollection<EqAppointment> Appointments { get; set; } = new List<EqAppointment>();
}
