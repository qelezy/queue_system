using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using WebApplication.Data;
using WebApplication.Services;
using Scalar.AspNetCore;
using System.Text;
using WebApplication.Models;

var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

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

            if (context.Request.Cookies.TryGetValue("accessToken", out var accessToken) && !string.IsNullOrWhiteSpace(accessToken))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

builder.Services.AddControllersWithViews();

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
    const string accessTokenCookieName = "accessToken";
    const string refreshTokenCookieName = "refreshToken";
    const string rememberMeCookieName = "rememberMe";

    if (context.Request.Cookies.TryGetValue(accessTokenCookieName, out var accessToken) &&
        IsJwtExpired(accessToken) &&
        context.Request.Cookies.TryGetValue(refreshTokenCookieName, out var refreshToken))
    {
        using var scope = app.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var rememberMeEnabled = context.Request.Cookies.TryGetValue(rememberMeCookieName, out var rememberValue) &&
                                rememberValue == "1";
        var refreshResult = await authService.RefreshTokenByTokenAsync(refreshToken, rememberMeEnabled);

        if (refreshResult.Succeeded && refreshResult.Data != null)
        {
            var isHttps = context.Request.IsHttps;
            context.Response.Cookies.Append(accessTokenCookieName, refreshResult.Data.AccessToken, BuildAuthCookieOptions(rememberMeEnabled ? refreshResult.Data.Expires : null, isHttps));
            context.Response.Cookies.Append(refreshTokenCookieName, refreshResult.Data.RefreshToken, BuildAuthCookieOptions(rememberMeEnabled ? DateTime.UtcNow.AddDays(jwtOptions.RefreshRememberDays) : null, isHttps));
            context.Response.Cookies.Append(rememberMeCookieName, rememberMeEnabled ? "1" : "0", BuildAuthCookieOptions(rememberMeEnabled ? DateTime.UtcNow.AddDays(jwtOptions.RefreshRememberDays) : null, isHttps));

            context.Items["accessToken"] = refreshResult.Data.AccessToken;
        }
        else
        {
            context.Response.Cookies.Delete(accessTokenCookieName);
            context.Response.Cookies.Delete(refreshTokenCookieName);
            context.Response.Cookies.Delete(rememberMeCookieName);
        }
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
}

app.Run();

static bool IsJwtExpired(string token)
{
    if (string.IsNullOrWhiteSpace(token))
        return true;

    try
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return jwt.ValidTo <= DateTime.UtcNow;
    }
    catch
    {
        return true;
    }
}

static CookieOptions BuildAuthCookieOptions(DateTime? expiresUtc, bool isHttps)
{
    return new CookieOptions
    {
        HttpOnly = true,
        Secure = isHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expiresUtc
    };
}
