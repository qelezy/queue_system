namespace WebApplication.Models;

public class UserRowViewModel
{
    public string Id { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string? Patronymic { get; set; }

    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string Role { get; set; } = "Dispatcher";

    public string RoleName { get; set; } = "";
}
