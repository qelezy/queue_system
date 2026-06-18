namespace WebApplication.Models.Emails;

public sealed class ChangeEmailEmailViewModel : IEmailPageViewModel
{
    public string Title { get; init; } = "Подтверждение смены почты";

    public string CurrentEmail { get; init; } = "";

    public string NewEmail { get; init; } = "";

    public string ConfirmationLink { get; init; } = "";
}
