namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqCabinet
{
    public int IdCabinet { get; set; }
    public string CabinetNumber { get; set; } = "";

    public ICollection<EqListItem> ListItems { get; set; } = new List<EqListItem>();
    public ICollection<EqLogWork> LogWorks { get; set; } = new List<EqLogWork>();
}
