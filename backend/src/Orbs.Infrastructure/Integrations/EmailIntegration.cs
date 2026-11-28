using System.Globalization;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;
using Orbs.Application.Abstractions;
using Orbs.Application.Tools;

namespace Orbs.Infrastructure.Integrations;

/// <summary>
/// An email account: read the inbox over IMAP, and send over SMTP when a server is set.
/// Sending is a "write" tool, so orbs ask before every message unless you allow it.
/// </summary>
internal sealed class EmailIntegration : IIntegrationKind
{
    private const int MaxBodyCharacters = 8_000;

    public string Kind => "email";

    public string DisplayName => "Email";

    public string Description => "Read your inbox over IMAP and, if you add an SMTP server, send email (orbs ask first).";

    public IReadOnlyList<IntegrationField> Fields { get; } =
    [
        new("imapHost", "IMAP server", Required: true, Placeholder: "imap.example.com"),
        new("imapPort", "IMAP port", Placeholder: "993"),
        new("username", "User name", Required: true),
        new("password", "Password", Secret: true, Hint: "Use an app password where your provider offers one."),
        new("smtpHost", "SMTP server", Placeholder: "smtp.example.com", Hint: "Leave empty to only read email."),
        new("smtpPort", "SMTP port", Placeholder: "587"),
        new("from", "Send as", Placeholder: "you@example.com"),
    ];

    public IReadOnlyList<ITool> CreateTools(IntegrationConfig config)
    {
        var tools = new List<ITool>
        {
            new DelegateTool(
                "email_list_recent",
                "List recent email",
                "List the most recent messages in the inbox: id, date, sender and subject.",
                """{"type":"object","properties":{"count":{"type":"integer","description":"1 to 25, default 10."},"unreadOnly":{"type":"boolean"}}}""",
                ToolRisk.Read,
                (args, ct) => ListAsync(config, args.Int("count", 10, 1, 25), args.Bool("unreadOnly"), ct)),
            new DelegateTool(
                "email_read",
                "Read an email",
                "Read one message from the inbox by the id that email_list_recent returned.",
                """{"type":"object","properties":{"id":{"type":"string"}},"required":["id"]}""",
                ToolRisk.Read,
                (args, ct) => ReadAsync(config, args.Required("id"), ct)),
        };

        if (config.Find("smtpHost") is not null)
        {
            tools.Add(new DelegateTool(
                "email_send",
                "Send email",
                "Send a plain-text email.",
                """{"type":"object","properties":{"to":{"type":"string"},"subject":{"type":"string"},"body":{"type":"string"}},"required":["to","subject","body"]}""",
                ToolRisk.Write,
                (args, ct) => SendAsync(config, args.Required("to"), args.Required("subject"), args.Required("body"), ct)));
        }

        return tools;
    }

    public async Task TestAsync(IntegrationConfig config, CancellationToken cancellationToken = default)
    {
        using var imap = await ConnectImapAsync(config, cancellationToken);
        await imap.DisconnectAsync(true, cancellationToken);
    }

    private static async Task<string> ListAsync(IntegrationConfig config, int count, bool unreadOnly, CancellationToken cancellationToken)
    {
        using var imap = await ConnectImapAsync(config, cancellationToken);
        var inbox = imap.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        var ids = await inbox.SearchAsync(unreadOnly ? SearchQuery.NotSeen : SearchQuery.All, cancellationToken);
        var recent = ids.Reverse().Take(count).ToList();
        if (recent.Count == 0)
        {
            return unreadOnly ? "No unread email." : "The inbox is empty.";
        }

        var summaries = await inbox.FetchAsync(recent, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId | MessageSummaryItems.Flags, cancellationToken);
        var text = new StringBuilder();
        foreach (var m in summaries.Where(s => s.Envelope is not null).OrderByDescending(s => s.Envelope!.Date))
        {
            var unread = m.Flags?.HasFlag(MessageFlags.Seen) == false ? " [unread]" : string.Empty;
            text.Append("id ").Append(m.UniqueId.Id.ToString(CultureInfo.InvariantCulture)).Append(" · ")
                .Append(m.Envelope!.Date?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" · ")
                .Append(m.Envelope.From?.ToString() ?? "unknown").Append(" · ")
                .Append(m.Envelope.Subject ?? "(no subject)").AppendLine(unread);
        }

        await imap.DisconnectAsync(true, cancellationToken);
        return text.ToString().TrimEnd();
    }

    private static async Task<string> ReadAsync(IntegrationConfig config, string id, CancellationToken cancellationToken)
    {
        if (!uint.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
        {
            throw new ArgumentException($"'{id}' is not a message id.");
        }

        using var imap = await ConnectImapAsync(config, cancellationToken);
        await imap.Inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        var message = await imap.Inbox.GetMessageAsync(new UniqueId(uid), cancellationToken);
        await imap.DisconnectAsync(true, cancellationToken);
        return Describe(message);
    }

    internal static string Describe(MimeMessage message)
    {
        var body = message.TextBody ?? (message.HtmlBody is { } html ? Context.UrlConnector.HtmlToText(html) : string.Empty);
        return new StringBuilder()
            .Append("From: ").AppendLine(message.From.ToString())
            .Append("To: ").AppendLine(message.To.ToString())
            .Append("Date: ").AppendLine(message.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append("Subject: ").AppendLine(message.Subject)
            .AppendLine()
            .Append(IntegrationKit.Clip(body.Trim(), MaxBodyCharacters))
            .ToString();
    }

    private static async Task<string> SendAsync(IntegrationConfig config, string to, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config.Find("from") ?? config.Get("username")));
        message.To.AddRange(InternetAddressList.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var smtp = new SmtpClient();
        var port = Port(config, "smtpPort", 587);
        await IntegrationKit.EnsureReachableAsync(config, config.Get("smtpHost"), cancellationToken);
        await smtp.ConnectAsync(config.Get("smtpHost"), port, port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
        await smtp.AuthenticateAsync(config.Get("username"), config.Secret ?? string.Empty, cancellationToken);
        await smtp.SendAsync(message, cancellationToken);
        await smtp.DisconnectAsync(true, cancellationToken);
        return $"Sent \"{subject}\" to {to}.";
    }

    private static async Task<ImapClient> ConnectImapAsync(IntegrationConfig config, CancellationToken cancellationToken)
    {
        var imap = new ImapClient();
        try
        {
            var port = Port(config, "imapPort", 993);
            await IntegrationKit.EnsureReachableAsync(config, config.Get("imapHost"), cancellationToken);
            await imap.ConnectAsync(config.Get("imapHost"), port, port == 993 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
            await imap.AuthenticateAsync(config.Get("username"), config.Secret ?? string.Empty, cancellationToken);
            return imap;
        }
        catch
        {
            imap.Dispose();
            throw;
        }
    }

    private static int Port(IntegrationConfig config, string key, int fallback) =>
        int.TryParse(config.Find(key), NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536 ? port : fallback;
}
