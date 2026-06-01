namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqSpecialty
{
    public int IdSpecialty { get; set; }
    public string Definition { get; set; } = "";
    
    public int TimeServicing { get; set; }

    public ICollection<EqListItem> ListItems { get; set; } = new List<EqListItem>();
}
