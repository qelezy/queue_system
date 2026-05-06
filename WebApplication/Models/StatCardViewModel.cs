namespace WebApplication.Models
{
    public class StatCardViewModel
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Unit { get; set; }
        public string Hint { get; set; }

        /// <summary>Вторая строка (например максимум за период).</summary>
        public string? SubLabel { get; set; }
        public string? SubValue { get; set; }
        public string? SubUnit { get; set; }
    }
}
