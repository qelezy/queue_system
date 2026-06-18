namespace WebApplication.Dto.Users;

public sealed class UserUpdateResultDto
{
    public UserDto User { get; init; } = new();
    public EmailChangeConfirmationDto? EmailChange { get; init; }
}
