using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace SKAuto.Core.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfigurationService _configService;
        private readonly ILoggingService _logger;

        public SmtpEmailService(IConfigurationService configService, ILoggingService logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var config = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
                var emailConfig = config.Email;

                if (string.IsNullOrEmpty(emailConfig.SenderEmail) || string.IsNullOrEmpty(emailConfig.SenderPassword))
                {
                    _logger.LogWarning("Email not configured – cannot send reset email.");
                    return;
                }

                using var client = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
                {
                    EnableSsl = emailConfig.EnableSsl,
                    Credentials = new NetworkCredential(emailConfig.SenderEmail, emailConfig.SenderPassword)
                };

                var mail = new MailMessage
                {
                    From = new MailAddress(emailConfig.SenderEmail, emailConfig.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mail.To.Add(to);

                await client.SendMailAsync(mail);
                _logger.LogInfo($"Password reset email sent to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email to {to}", ex);
                throw;
            }
        }
    }
}
