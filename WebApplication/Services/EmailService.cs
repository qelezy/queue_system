using System.Net.Mail;

namespace MyWebApplication.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string body)
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = _configuration.GetValue<int>("Email:SmtpPort");
            var smtpUser = _configuration["Email:SmtpUser"];
            var smtpPass = _configuration["Email:SmtpPass"];
            var fromEmail = _configuration["Email:From"];

            var message = new MailMessage();
            message.To.Add(email);
            message.From = new MailAddress(fromEmail!);
            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            var client = new SmtpClient(smtpHost, smtpPort);

            await client.SendMailAsync(message);
        }
    }
}
