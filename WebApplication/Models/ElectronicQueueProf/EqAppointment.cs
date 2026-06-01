namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqAppointment
{
    public int IdAppointment { get; set; }
    public int? IdCategory { get; set; }
    public DateOnly DateArrival { get; set; }
    public TimeOnly TimeArrival { get; set; }
    public string? Number { get; set; }
    public TimeOnly? TimeStartPause { get; set; }
    public int Priority { get; set; }
    public string Info { get; set; } = "";
    public int? IdClient { get; set; }
    public TimeOnly? TimeComplete { get; set; }

    public EqCategory Category { get; set; } = null!;
    public ICollection<EqListItem> ListItems { get; set; } = new List<EqListItem>();
}
