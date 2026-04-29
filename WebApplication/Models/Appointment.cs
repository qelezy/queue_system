namespace WebApplication.Models
{
    public class Appointment
    {
        public long Id { get; set; }
        public long QueueEntryId { get; set; }
        public long DoctorId { get; set; }
        public long CabinetId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public QueueEntry QueueEntry { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public Cabinet Cabinet { get; set; } = null!;
    }
}
