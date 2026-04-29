namespace WebApplication.Models
{
    public class ServiceCategory
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Priority { get; set; }

        public ICollection<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();
    }
}
