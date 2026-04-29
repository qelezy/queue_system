using System.ComponentModel.DataAnnotations;

namespace WebApplication.Models
{
    public class RegisterUserViewModel
    {
        [Required]
        [Display(Name = "Фамилия")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Имя")]
        public string FirstName { get; set; }

        [Display(Name = "Отчество")]
        public string Patronymic { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Роль")]
        public string Role { get; set; }
    }
}