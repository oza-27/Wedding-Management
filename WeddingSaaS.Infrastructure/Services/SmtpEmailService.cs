using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WeddingSaaS.Application.Abstractions.Email;

namespace WeddingSaaS.Infrastructure.Services;

public sealed class SmtpEmailService(IConfiguration configuration, IHostEnvironment environment) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var host = configuration["Email:Smtp:Host"];
        var sender = configuration["Email:SenderEmail"];
        var portText = configuration["Email:Smtp:Port"];
        if (!string.Equals(configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Email:Provider must be set to the implemented SMTP provider.");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) || !int.TryParse(portText, out var port))
            throw new InvalidOperationException("Email SMTP host, port, and sender must be configured before email delivery is used.");
        var useSsl = bool.TryParse(configuration["Email:Smtp:EnableSsl"], out var enableSsl) && enableSsl;
        if (!environment.IsDevelopment() && !useSsl) throw new InvalidOperationException("SMTP TLS must be enabled outside Development.");

        using var mail = new MailMessage
        {
            From = new MailAddress(sender, configuration["Email:SenderName"] ?? "Wedding SaaS"),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl
        };
        var username = configuration["Email:Smtp:Username"];
        var password = configuration["Email:Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password);
        await client.SendMailAsync(mail, cancellationToken);
    }
}
