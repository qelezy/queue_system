namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqSettingQueue
{
    public int IdSetting { get; set; }
    public string? Name { get; set; }
    public int? StartIdSpecialty { get; set; }
    public int? EndIdSpecialty { get; set; }
    public int TimePause { get; set; }
    public int CriticalNumPause { get; set; }

    public ICollection<EqCategory> Categories { get; set; } = new List<EqCategory>();
}
