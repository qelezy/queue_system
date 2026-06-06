namespace WebApplication.Models.ElectronicQueueProf;

public sealed class EqLogWork
{
    public int IdLogWork { get; set; }
    public int IdCabinet { get; set; }
    public int IdDoctor { get; set; }
    public DateOnly DateWork { get; set; }
    public TimeOnly? TimeBegin { get; set; }
    public TimeOnly? TimeEnd { get; set; }

    public EqCabinet Cabinet { get; set; } = null!;
    public EqDoctor Doctor { get; set; } = null!;
}
