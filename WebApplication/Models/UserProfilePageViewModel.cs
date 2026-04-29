namespace WebApplication.Models
{
    public class UserProfilePageViewModel
    {
        public UserProfileViewModel Profile { get; set; } = new();
        public ChangePasswordViewModel Password { get; set; } = new();
    }
}
