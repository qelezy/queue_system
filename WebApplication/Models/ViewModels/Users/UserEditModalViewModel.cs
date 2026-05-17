namespace WebApplication.Models.ViewModels.Users {
    public class UserEditModalViewModel
    {
        public string ModalId { get; set; } = "edit-user-modal";
        public string Title { get; set; } = "Редактирование пользователя";
        public EditUserViewModel FormModel { get; set; } = new();
    }
}
