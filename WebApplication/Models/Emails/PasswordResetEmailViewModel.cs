namespace WebApplication.Models.Emails;

public sealed class PasswordResetEmailViewModel : IEmailPageViewModel
{
    public string Title { get; init; } = "Сброс пароля";

    public string ResetLink { get; init; } = "";
}
