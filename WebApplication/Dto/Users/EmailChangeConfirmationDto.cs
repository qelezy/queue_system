namespace WebApplication.Dto.Users;

public sealed class EmailChangeConfirmationDto
{
    public string UserId { get; init; } = string.Empty;
    public string CurrentEmail { get; init; } = string.Empty;
    public string NewEmail { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}
