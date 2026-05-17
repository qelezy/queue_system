namespace WebApplication.Models.ViewModels.Shared {
    public class ToastItemViewModel
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = "error";
    }

    public class ToastStackViewModel
    {
        public string ContainerId { get; set; } = "app-toast-stack";
        public string? SuccessMessage { get; set; }
        public int MaxToasts { get; set; } = 3;
        public int AutoCloseMs { get; set; } = 3000;
        public IReadOnlyList<ToastItemViewModel> Items { get; set; } = Array.Empty<ToastItemViewModel>();
    }
}
