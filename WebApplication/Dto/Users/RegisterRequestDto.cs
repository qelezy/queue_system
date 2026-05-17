namespace WebApplication.Dto.Users {
    public class RegisterRequestDto
    {
        public string FirstName { get; set; } = string.Empty;
		public string LastName { get; set; } = string.Empty;
		public string Patronymic { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "Dispatcher";
    }
}
