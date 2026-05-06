namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqStatusItemList
{
    public int IdStatusItem { get; set; }
    public string Name { get; set; } = "";

    public ICollection<EqListItem> ListItems { get; set; } = new List<EqListItem>();
}
