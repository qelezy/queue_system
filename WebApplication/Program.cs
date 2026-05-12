using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApplication.Data;
using WebApplication.Services;
using Scalar.AspNetCore;
using System.Text;
using WebApplication.Models;
using WebApplication.Services.Reports;
using WebApplication.Services.Reports.LoadAndDowntime;

Env.TraversePath().Load();

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var userDatabaseConnection = builder.Configuration.GetConnectionString("UserDatabase")
    ?? throw new InvalidOperationException("Строка подключения UserDatabase не задана.");
var electronicQueueConnection = builder.Configuration.GetConnectionString("ElectronicQueue")
    ?? throw new InvalidOperationException("Строка подключения ElectronicQueue не задана.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(userDatabaseConnection));

builder.Services.AddDbContext<ElectronicQueueDbContext>(options =>
    options
        .UseSqlServer(electronicQueueConnection)
        .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["AppSettings:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["AppSettings:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(5),
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["AppSettings:Token"]!)),
        ValidateIssuerSigningKey = true
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.HttpContext.Items.TryGetValue("accessToken", out var runtimeTokenObj) &&
                runtimeTokenObj is string runtimeToken &&
                !string.IsNullOrWhiteSpace(runtimeToken))
            {
                context.Token = runtimeToken;
                return Task.CompletedTask;
            }

            if (context.Request.Cookies.TryGetValue(AuthCookieHelper.AccessTokenCookieName, out var accessToken) && !string.IsNullOrWhiteSpace(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddControllersWithViews();
builder.Services.Configure<AntiforgeryOptions>(o =>
{
    o.HeaderName = "RequestVerificationToken";
});

builder.Services.AddScoped<IPasswordGeneratorService, PasswordGeneratorService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IUserService,  UserService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    //options.LowercaseQueryStrings = true;
});
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("JwtOptions"));
builder.Services.Configure<ReportsOptions>(builder.Configuration.GetSection("Reports"));
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection(MonitoringOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IReportsCatalog, ReportsCatalog>();
builder.Services.AddScoped<IElectronicQueueAvailability, ElectronicQueueAvailabilityService>();
builder.Services.AddScoped<QueueDashboardService>();
builder.Services.AddScoped<MockQueueDashboardService>();
builder.Services.AddScoped<IQueueDashboardService, ResilientQueueDashboardService>();
builder.Services.AddScoped<IReportGenerator, LoadAndDowntimeReportGenerator>();
builder.Services.AddScoped<ReportGeneratorRegistry>();
builder.Services.AddScoped<ReportGenerationService>();
builder.Services.AddScoped<MockReportGenerationService>();
builder.Services.AddScoped<IReportGenerationService, ResilientReportGenerationService>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();

var app = builder.Build();
var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    if (!context.Request.Cookies.TryGetValue(AuthCookieHelper.RefreshTokenCookieName, out var refreshToken) ||
        string.IsNullOrWhiteSpace(refreshToken))
    {
        await next();
        return;
    }

    context.Request.Cookies.TryGetValue(AuthCookieHelper.AccessTokenCookieName, out var accessToken);
    if (!AccessTokenRefreshGate.ShouldTryRefresh(accessToken))
    {
        await next();
        return;
    }

    using var scope = app.Services.CreateScope();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var refreshResult = await authService.RefreshTokenByTokenAsync(refreshToken);

    if (refreshResult.Succeeded && refreshResult.Data != null)
    {
        AuthCookieHelper.AppendAuthCookies(context.Response, refreshResult.Data, jwtOptions, context.Request.IsHttps);
        context.Items["accessToken"] = refreshResult.Data.AccessToken;
    }
    else
    {
        AuthCookieHelper.DeleteAuthCookies(context.Response);
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "Manager", "Registrator" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    var permissionService = scope.ServiceProvider.GetRequiredService<IRolePermissionService>();
    await permissionService.SyncPermissionsAndSeedDefaultsAsync();
}

app.Run();
