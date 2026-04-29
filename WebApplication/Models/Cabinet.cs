namespace WebApplication.Models
{
    public class Cabinet
    {
        public long Id { get; set; }
        public string CabinetNumber { get; set; } = string.Empty;

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
