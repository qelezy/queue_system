namespace WebApplication.Models.ViewModels.Dashboard {
    public class StatCardViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;

        /// <summary>Вторая строка (например максимум за период).</summary>
        public string? SubLabel { get; set; }
        public string? SubValue { get; set; }
        public string? SubUnit { get; set; }
    }
}
