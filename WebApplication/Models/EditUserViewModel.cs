using System.ComponentModel.DataAnnotations;

namespace WebApplication.Models
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Имя")]
        public string FirstName { get; set; } = string.Empty;

        [Display(Name = "Отчество")]
        public string? Patronymic { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Роль")]
        public string Role { get; set; } = "Dispatcher";
    }
}
