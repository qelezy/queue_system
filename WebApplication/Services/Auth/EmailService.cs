using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using WebApplication.Models.Configuration;

namespace WebApplication.Services.Auth;

public class EmailService : IEmailService
{
    private readonly EmailOptions _options;

    public EmailService(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendEmailAsync(string email, string subject, string body)
    {
        using var message = new MailMessage();
        message.To.Add(email);
        message.From = new MailAddress(_options.From);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = true;

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPass);
        }

        await client.SendMailAsync(message).ConfigureAwait(false);
    }
}
