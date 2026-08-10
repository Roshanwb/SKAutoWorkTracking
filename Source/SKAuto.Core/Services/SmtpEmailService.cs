using SKAuto.Core.DTOs;
using SKAuto.Core.Interfaces;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
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
            await SendEmailAsync(to, subject, body, null);
        }

        public async Task SendEmailAsync(string to, string subject, string body, IEnumerable<string> attachmentPaths)
        {
            try
            {
                var config = await _configService.GetAsync<AppConfig>("AppConfig") ?? new AppConfig();
                var emailConfig = config.Email;

                if (string.IsNullOrEmpty(emailConfig.SenderEmail) || string.IsNullOrEmpty(emailConfig.SenderPassword))
                {
                    _logger.LogWarning("Email not configured – cannot send email.");
                    return;
                }

                using var client = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort)
                {
                    EnableSsl = emailConfig.EnableSsl,
                    Credentials = new NetworkCredential(emailConfig.SenderEmail, emailConfig.SenderPassword)
                };

                using var mail = new MailMessage
                {
                    From = new MailAddress(emailConfig.SenderEmail, emailConfig.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };
                mail.To.Add(to);

                // Add attachments if any
                if (attachmentPaths != null)
                {
                    foreach (var path in attachmentPaths)
                    {
                        if (System.IO.File.Exists(path))
                        {
                            var attachment = new Attachment(path);
                            mail.Attachments.Add(attachment);
                        }
                        else
                        {
                            _logger.LogWarning($"Attachment file not found: {path}");
                        }
                    }
                }

                await client.SendMailAsync(mail);
                _logger.LogInfo($"Email sent to {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to send email to {to}", ex);
                throw;
            }
        }
    }
}