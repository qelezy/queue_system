namespace WebApplication.Models.ViewModels.Profile {
    public class UserProfilePageViewModel
    {
        public UserProfileViewModel Profile { get; set; } = new();
        public ChangePasswordViewModel Password { get; set; } = new();
    }
}
