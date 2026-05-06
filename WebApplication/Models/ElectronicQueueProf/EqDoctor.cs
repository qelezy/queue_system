namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqDoctor
{
    public int IdDoctor { get; set; }
    public string FullName { get; set; } = "";

    public ICollection<EqListItem> ListItems { get; set; } = new List<EqListItem>();
    public ICollection<EqLogWork> LogWorks { get; set; } = new List<EqLogWork>();
}
