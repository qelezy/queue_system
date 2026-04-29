namespace WebApplication.Models
{
    public class QueueEntry
    {
        public long Id { get; set; }
        public long PatientId { get; set; }
        public long ServiceCategoryId { get; set; }
        public string Status { get; set; } = "waiting";
        public DateTime QueuedAt { get; set; }
        public DateTime? CalledAt { get; set; }

        public Patient Patient { get; set; } = null!;
        public ServiceCategory ServiceCategory { get; set; } = null!;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
