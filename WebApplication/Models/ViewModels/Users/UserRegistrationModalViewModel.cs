namespace WebApplication.Models.ViewModels.Users {
    public class UserRegistrationModalViewModel
    {
        public string ModalId { get; set; } = "register-user-modal";
        public string Title { get; set; } = "Регистрация пользователя";
        public RegisterUserViewModel FormModel { get; set; } = new();
    }
}
