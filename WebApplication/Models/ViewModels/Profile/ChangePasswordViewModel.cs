using System.ComponentModel.DataAnnotations;

namespace WebApplication.Models.ViewModels.Profile {
    public class ChangePasswordViewModel
    {
        [DataType(DataType.Password)]
        [Display(Name = "Текущий пароль")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Новый пароль")]
        public string? NewPassword { get; set; }
    }
}
