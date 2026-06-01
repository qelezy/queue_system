using Microsoft.AspNetCore.Identity;
using WebApplication.Models.Identity;

namespace WebApplication.Configuration;

public static class DockerBootstrapSeedExtensions
{
    public static async Task SeedDockerBootstrapAdminAsync(this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var configuration = app.Configuration;
        if (!configuration.GetValue("DOCKER_BOOTSTRAP_ADMIN", false))
            return;

        var email = configuration["DOCKER_BOOTSTRAP_ADMIN_EMAIL"];
        var password = configuration["DOCKER_BOOTSTRAP_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("WebApplication.DockerBootstrap");

        if (await userManager.FindByEmailAsync(email).ConfigureAwait(false) is not null)
            return;

        var user = new User
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            FirstName = "Admin",
            LastName = "Docker"
        };

        var result = await userManager.CreateAsync(user, password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Docker bootstrap admin was not created: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, "Admin").ConfigureAwait(false);
        logger.LogInformation("Docker bootstrap admin created for {Email}", email);
    }
}
