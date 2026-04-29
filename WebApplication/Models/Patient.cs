namespace WebApplication.Models
{
    public class Patient
    {
        public long Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Patronymic { get; set; }

        public ICollection<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();
    }
}
