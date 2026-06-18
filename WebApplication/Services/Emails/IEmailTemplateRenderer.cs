using WebApplication.Models.Emails;

namespace WebApplication.Services.Emails;

public interface IEmailTemplateRenderer
{
    Task<string> RenderRegistrationAsync(RegistrationEmailViewModel model);

    Task<string> RenderPasswordResetAsync(PasswordResetEmailViewModel model);

    Task<string> RenderChangeEmailAsync(ChangeEmailEmailViewModel model);
}
