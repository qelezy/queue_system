namespace WebApplication.Models.ViewModels.Users {
    public class UsersPageViewModel
    {
        public IReadOnlyList<UserRowViewModel> Users { get; set; } = Array.Empty<UserRowViewModel>();
        public RegisterUserViewModel RegisterUser { get; set; } = new();
        public AccessSettingsViewModel AccessSettings { get; set; } = new();
    }
}
