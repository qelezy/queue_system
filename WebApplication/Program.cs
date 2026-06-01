using DotNetEnv;
using QuestPDF.Infrastructure;
using Scalar.AspNetCore;
using WebApplication.Configuration.DependencyInjection;
using WebApplication.Hubs;
using WebApplication.Middleware;

Env.TraversePath().Load();

QuestPDF.Settings.License = LicenseType.Community;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

builder.Services.AddWebApplicationServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.ValidateReportsConfiguration();
app.ValidateMonitoringConfiguration();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAccessTokenRefresh();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.MapHub<DashboardHub>("/hubs/dashboard");

await app.ValidateWebApplicationDatabasesAsync().ConfigureAwait(false);
await app.SeedWebApplicationDataAsync().ConfigureAwait(false);

app.Run();
