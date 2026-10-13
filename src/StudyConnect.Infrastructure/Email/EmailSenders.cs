using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using StudyConnect.Core.Abstractions;
using StudyConnect.Core.Models;
using StudyConnect.Core.Validation;

namespace StudyConnect.Infrastructure.Email;

/// <summary>E-mail settings bound from the "Email" section of appsettings.json.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? SmtpHost { get; set; }

    public int SmtpPort { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@studyconnect.local";

    public string FromName { get; set; } = "StudyConnect";

    /// <summary>Folder used by <see cref="FileOutboxEmailSender"/> when SMTP is not configured.</summary>
    public string OutboxDirectory { get; set; } = "App_Data/outbox";

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}

/// <summary>
/// Template-method base class for e-mail senders: validation and logging live here once,
/// while each subclass overrides <see cref="DeliverAsync"/> with its own delivery mechanism
/// (polymorphism through inheritance and overriding).
/// </summary>
public abstract class EmailSenderBase : IEmailSender
{
    protected EmailSenderBase(EmailOptions options, ILogger logger)
    {
        Options = options;
        Logger = logger;
    }

    protected EmailOptions Options { get; }

    protected ILogger Logger { get; }

    public abstract bool DeliversExternally { get; }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!InputValidators.IsValidEmail(message.To))
        {
            throw new ArgumentException($"'{message.To}' is not a valid e-mail address.", nameof(message));
        }

        await DeliverAsync(message, cancellationToken);
        Logger.LogInformation("E-mail '{Subject}' sent to {To} via {Sender}", message.Subject, message.To, GetType().Name);
    }

    protected abstract Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Sends real e-mails through an SMTP server (e.g. Gmail with an app password).</summary>
public sealed class SmtpEmailSender : EmailSenderBase
{
    public SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger)
        : base(options, logger)
    {
    }

    public override bool DeliversExternally => true;

    protected override async Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        using var mail = new MailMessage
        {
            From = new MailAddress(Options.FromAddress, Options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);

        using var client = new SmtpClient(Options.SmtpHost, Options.SmtpPort)
        {
            EnableSsl = Options.EnableSsl,
            Credentials = string.IsNullOrEmpty(Options.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(Options.Username, Options.Password)
        };

        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>
/// Development e-mail sender: writes each e-mail as an HTML file into the outbox folder
/// (file writing) so the app works in the lab without any mail server.
/// </summary>
public sealed class FileOutboxEmailSender : EmailSenderBase
{
    public FileOutboxEmailSender(EmailOptions options, ILogger<FileOutboxEmailSender> logger)
        : base(options, logger)
    {
    }

    public override bool DeliversExternally => false;

    protected override async Task DeliverAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Options.OutboxDirectory);

        string safeRecipient = string.Concat(message.To.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        string fileName = string.Create(CultureInfo.InvariantCulture, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}_{safeRecipient}.html");
        string path = Path.Combine(Options.OutboxDirectory, fileName);

        var html = new StringBuilder()
            .AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>")
            .AppendLine(WebUtility.HtmlEncode(message.Subject))
            .AppendLine("</title></head><body style=\"font-family:sans-serif\">")
            .AppendLine(CultureInfo.InvariantCulture, $"<p><b>To:</b> {WebUtility.HtmlEncode(message.To)}<br/><b>Subject:</b> {WebUtility.HtmlEncode(message.Subject)}</p><hr/>")
            .AppendLine(message.HtmlBody)
            .AppendLine("</body></html>");

        await File.WriteAllTextAsync(path, html.ToString(), Encoding.UTF8, cancellationToken);
        Logger.LogInformation("Development mode: e-mail written to {Path}", path);
    }
}
