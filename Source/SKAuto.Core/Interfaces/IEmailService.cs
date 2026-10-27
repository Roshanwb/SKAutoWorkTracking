using System.Collections.Generic;
using System.Threading.Tasks;

namespace SKAuto.Core.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendEmailAsync(string to, string subject, string body, IEnumerable<string> attachmentPaths);
    }
}