namespace WebApplication.Models.Emails;

public sealed class RegistrationEmailViewModel : IEmailPageViewModel
{
    public string Title { get; init; } = "Подтверждение почты";

    public string Password { get; init; } = "";

    public string ConfirmationLink { get; init; } = "";
}
